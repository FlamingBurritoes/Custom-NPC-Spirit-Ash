using System.Globalization;
using System.Security.Cryptography;
using System.Xml.Linq;

namespace SpiritCreator;

public static class RegulationExporter
{
    private const string StockHash = "B8CDAB84C0772F66811FDC5176A381877CA6657B37D420CC2C1562A90D63870E";
    private const int Jellyfish = 23600000, Nepheli = 25900000, CustomRow = 99000;
    private static readonly string[] ModifiedTables = ["BuddyParam", "CharaInitParam", "FaceParam"];

    public static string Export(SpiritProfile profile, string regulation, string witchy, string output)
    {
        profile.Validate();
        if (!OperatingSystem.IsWindows())
            throw new InvalidOperationException("The alpha exporter needs Windows and WitchyBND 3.0.1.0.");
        regulation = Path.GetFullPath(regulation);
        output = Path.TrimEndingDirectorySeparator(Path.GetFullPath(output));
        var parent = Path.GetDirectoryName(output) ?? throw new ArgumentException("Choose a new output folder, not a drive root.");
        if (Directory.Exists(output) || File.Exists(output))
            throw new IOException($"Output already exists: {output}. Use a new folder.");
        if (!File.Exists(regulation))
            throw new FileNotFoundException("Pass the stock regulation.bin from Elden Ring's Game folder.", regulation);
        if (Hash(regulation) != StockHash)
            throw new InvalidDataException("Unsupported or modified regulation.bin. The alpha accepts only the verified Steam 1.17.3 file (11730034).");

        using var scratch = Directory.CreateTempSubdirectory("spirit-export-").AsScratch();
        var tool = Witchy.CreatePrivateCopy(witchy, Path.Combine(scratch.Path, "tool"));
        var regulationCopy = Path.Combine(scratch.Path, "regulation.bin");
        File.Copy(regulation, regulationCopy);
        if (Hash(regulationCopy) != StockHash)
            throw new IOException("The stock regulation changed during the copy.");
        tool.Run(regulationCopy, "unpack");
        var binder = Path.Combine(scratch.Path, "regulation-bin");
        var manifestPath = Path.Combine(binder, "_witchy-bnd4.xml");
        var manifest = ReadManifest(manifestPath);
        var stock = Fingerprint(binder);
        if (stock.Count != 194)
            throw new InvalidDataException("Unexpected number of stock parameter tables.");

        ParamXml ReadTable(string name)
        {
            var path = Path.Combine(binder, name + ".param");
            tool.Run(path, "read " + name);
            return ParamXml.Load(path + ".xml");
        }

        var buddy = ReadTable("BuddyParam");
        var chara = ReadTable("CharaInitParam");
        var face = ReadTable("FaceParam");
        var weapons = ReadTable("EquipParamWeapon");
        var target = buddy.Find(Jellyfish);
        var donor = buddy.Find(Nepheli);
        if (buddy.Value(target, "triggerSpEffectId") != "236000" ||
            buddy.Value(target, "npcParamId") != "141800000" ||
            buddy.Value(donor, "npcParamId") != "100000020" ||
            buddy.Value(donor, "npcThinkParamId") != "100000020" ||
            buddy.Value(donor, "npcPlayerInitParamId") != "23342")
            throw new InvalidDataException("The stock Jellyfish/Nepheli rows did not match the tested mapping.");

        var characterTemplate = chara.Find(23342);
        var weapon = weapons.Find(profile.WeaponId);
        var stockWeapon = weapons.Find(14140000);
        foreach (var field in new[] { "wepType", "weaponCategory", "wepmotionCategory" })
            if (weapons.Value(stockWeapon, field) != weapons.Value(weapon, field))
                throw new InvalidDataException("This weapon does not match Nepheli's tested weapon class.");
        foreach (var (requirement, stat) in new[] { ("properStrength", "baseStr"), ("properAgility", "baseDex"),
                     ("properMagic", "baseMag"), ("properFaith", "baseFai"), ("properLuck", "baseLuc") })
            if (int.Parse(weapons.Value(weapon, requirement), CultureInfo.InvariantCulture) >
                int.Parse(chara.Value(characterTemplate, stat), CultureInfo.InvariantCulture))
                throw new InvalidDataException("The companion does not meet the requested weapon's stat requirements.");

        if (buddy.Rows.Any(row => buddy.Value(row, "npcPlayerInitParamId") == CustomRow.ToString(CultureInfo.InvariantCulture)) ||
            chara.Rows.Any(row => chara.Value(row, "npcPlayerFaceGenId") == CustomRow.ToString(CultureInfo.InvariantCulture)))
            throw new InvalidDataException("A stock row already references the alpha's custom ID.");
        foreach (var key in buddy.Fields)
            buddy.Set(target, key, buddy.Value(donor, key));
        buddy.Set(target, "triggerSpEffectId", "236000");
        buddy.Set(target, "npcPlayerInitParamId", CustomRow);

        var init = chara.AddClone(characterTemplate, CustomRow);
        chara.Set(init, "equip_Helm", -1);
        chara.Set(init, "npcPlayerSex", profile.BodyType);
        chara.Set(init, "npcPlayerFaceGenId", CustomRow);
        chara.Set(init, "equip_Wep_Right", profile.WeaponId);
        chara.Set(init, "equip_Wep_Left", profile.WeaponId);

        var appearance = face.AddClone(face.Find(1), CustomRow);
        foreach (var (key, value) in profile.Face)
            face.Set(appearance, key, value);

        var tables = new[] { buddy, chara, face };
        foreach (var table in tables)
        {
            table.Save();
            tool.Run(table.Path, "repack " + table.Name);
        }
        manifest.Root!.SetElementValue("compressionLevel", 15);
        manifest.Save(manifestPath);
        // The original copy is no longer needed. Deleting it makes output existence meaningful.
        File.Delete(regulationCopy);
        tool.Run(binder, "repack regulation");

        var verifyDir = Directory.CreateDirectory(Path.Combine(scratch.Path, "verify")).FullName;
        var verifiedPackage = Path.Combine(verifyDir, "regulation.bin");
        File.Copy(regulationCopy, verifiedPackage);
        tool.Run(verifiedPackage, "verify repacked regulation");
        var verifiedBinder = Path.Combine(verifyDir, "regulation-bin");
        ReadManifest(Path.Combine(verifiedBinder, "_witchy-bnd4.xml"));
        var rebuilt = Fingerprint(verifiedBinder);
        var changed = rebuilt.Where(pair => stock.TryGetValue(pair.Key, out var hash) && hash != pair.Value)
            .Select(pair => pair.Key).ToHashSet(StringComparer.Ordinal);
        if (!stock.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(rebuilt.Keys) ||
            !changed.SetEquals(ModifiedTables.Select(name => name + ".param")))
            throw new InvalidDataException("Verification found an unexpected set of modified tables.");
        foreach (var table in tables)
        {
            var param = Path.Combine(verifiedBinder, table.Name + ".param");
            tool.Run(param, "verify " + table.Name);
            table.Verify(ParamXml.Load(param + ".xml"));
        }
        if (Hash(verifiedPackage) != Hash(regulationCopy))
            throw new IOException("Repacked regulation changed during verification.");

        Directory.CreateDirectory(parent);
        var staging = Path.Combine(parent, ".spirit-staging-" + Guid.NewGuid().ToString("N"));
        using var pending = Directory.CreateDirectory(staging).AsScratch();
        Directory.CreateDirectory(Path.Combine(staging, "mod"));
        var stagedRegulation = Path.Combine(staging, "mod", "regulation.bin");
        File.Copy(verifiedPackage, stagedRegulation);
        if (Hash(stagedRegulation) != Hash(verifiedPackage))
            throw new IOException("Staged regulation hash did not match.");
        File.WriteAllText(Path.Combine(staging, "spirit.me3"), """
            profileVersion = "v1"
            savefile = "SpiritCreatorTest.sl2"
            start_online = false

            [[supports]]
            game = "eldenring"

            [[packages]]
            path = "mod"
            """ + "\n");
        Directory.Move(staging, output);
        return Path.Combine(output, "spirit.me3");
    }

    private static XDocument ReadManifest(string path)
    {
        var manifest = XDocument.Load(path);
        if (manifest.Root?.Name != "bnd4" || (string?)manifest.Root.Element("game") != "ER" ||
            (string?)manifest.Root.Element("version") != SpiritProfile.Regulation ||
            (string?)manifest.Root.Element("compression") != "DCX_ZSTD")
            throw new InvalidDataException("WitchyBND did not produce the expected 1.17.3 binder manifest.");
        return manifest;
    }

    private static Dictionary<string, string> Fingerprint(string dir) =>
        Directory.EnumerateFiles(dir, "*.param").ToDictionary(path => Path.GetFileName(path), Hash, StringComparer.Ordinal);

    private static string Hash(string path)
    {
        using var input = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(input));
    }
}

internal sealed class ParamXml
{
    private readonly XDocument _document;
    private readonly XElement _rows;
    private readonly Dictionary<int, XElement> _byId;
    private readonly Dictionary<string, string?> _defaults;

    private ParamXml(string path)
    {
        Path = path;
        _document = XDocument.Load(path);
        var root = _document.Root;
        if (root?.Name != "param" || (string?)root.Element("cellStyle") != "0")
            throw new InvalidDataException("WitchyBND must use the Attribute PARAM field style.");
        _rows = root.Element("rows") ?? throw new InvalidDataException("Missing PARAM rows.");
        _byId = _rows.Elements("row").ToDictionary(Id);
        _defaults = (root.Element("fields") ?? throw new InvalidDataException("Missing PARAM fields."))
            .Elements("field").ToDictionary(field => (string)field.Attribute("name")!,
                field => (bool?)field.Attribute("defaultThreshold") == true ? (string?)field.Attribute("defaultValue") : null,
                StringComparer.Ordinal);
    }

    internal string Path { get; }
    internal string Name => System.IO.Path.GetFileNameWithoutExtension(System.IO.Path.GetFileNameWithoutExtension(Path));
    internal IEnumerable<string> Fields => _defaults.Keys;
    internal IEnumerable<XElement> Rows => _byId.Values;
    internal static ParamXml Load(string path) => new(path);
    internal XElement Find(int id) => _byId.TryGetValue(id, out var row) ? row :
        throw new InvalidDataException($"Missing {Name} row {id}.");
    internal string Value(XElement row, string field) =>
        _defaults.TryGetValue(field, out var fallback) && ((string?)row.Attribute(field) ?? fallback) is { } value
            ? value : throw new InvalidDataException($"Missing {Name} field {field} for row {Id(row)}.");

    internal void Set(XElement row, string field, object value)
    {
        if (!_defaults.ContainsKey(field))
            throw new InvalidDataException($"Missing target field {Name}.{field}.");
        row.SetAttributeValue(field, value);
    }

    internal XElement AddClone(XElement template, int id)
    {
        if (_byId.ContainsKey(id))
            throw new InvalidDataException($"Custom {Name} row {id} is already in use.");
        var clone = new XElement(template);
        clone.SetAttributeValue("id", id);
        clone.SetAttributeValue("name", null);
        clone.SetAttributeValue("paramdexName", null);
        var next = _rows.Elements("row").FirstOrDefault(row => Id(row) > id);
        if (next is null)
            _rows.Add(clone);
        else
            next.AddBeforeSelf(clone);
        _byId.Add(id, clone);
        return clone;
    }

    internal void Save() => _document.Save(Path, SaveOptions.DisableFormatting);

    internal void Verify(ParamXml actual)
    {
        if (!_byId.Keys.ToHashSet().SetEquals(actual._byId.Keys) ||
            !Fields.ToHashSet(StringComparer.Ordinal).SetEquals(actual.Fields) ||
            !_rows.Elements("row").Select(Id).SequenceEqual(actual._rows.Elements("row").Select(Id)))
            throw new InvalidDataException($"Unexpected row IDs, order, or fields in {Name}.");
        foreach (var (id, expected) in _byId)
        {
            var row = actual.Find(id);
            foreach (var field in Fields)
                if (Value(expected, field) != actual.Value(row, field))
                    throw new InvalidDataException($"Repack changed {Name}[{id}].{field}.");
        }
    }

    private static int Id(XElement row) => (int)row.Attribute("id")!;
}
