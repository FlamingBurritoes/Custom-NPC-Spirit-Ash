using System.Buffers.Binary;
using System.Security.Cryptography;
using static SpiritCreator.RegulationBinary;

namespace SpiritCreator;

public static class RegulationExporter
{
    private const string StockHash = "B8CDAB84C0772F66811FDC5176A381877CA6657B37D420CC2C1562A90D63870E";
    private const int StockSize = 2045728;
    private const int Jellyfish = 23600000, Nepheli = 25900000, CustomRow = 99000;
    private const int EffectStart = 900000, StockEffectStart = 290050;
    private static readonly int[] DamageRates = [72, 76, 80, 84, 480];

    public static string Export(SpiritProfile profile, string regulation, string output)
    {
        profile.Validate();
        output = Path.TrimEndingDirectorySeparator(Path.GetFullPath(output));
        var parent = Path.GetDirectoryName(output) ?? throw new ArgumentException("Choose a new output folder, not a drive root.");
        if (Directory.Exists(output) || File.Exists(output))
            throw new IOException($"Output already exists: {output}. Use a new folder.");
        byte[] stock;
        using (var input = File.OpenRead(regulation))
        {
            Require(input.Length == StockSize, "Select the verified stock Steam 1.17.3 regulation.bin (11730034).");
            stock = new byte[StockSize];
            input.ReadExactly(stock);
        }
        Require(Convert.ToHexString(SHA256.HashData(stock)) == StockHash,
            "Unsupported or modified regulation.bin. Select the stock Steam 1.17.3 file (11730034).");
        var original = RegulationBinary.Read(stock);

        var buddy = new RegulationParam(original.Table("BuddyParam"), "BUDDY_PARAM_ST", 160);
        var characters = new RegulationParam(original.Table("CharaInitParam"), "CHARACTER_INIT_PARAM", 320);
        var faces = new RegulationParam(original.Table("FaceParam"), "FACE_PARAM_ST", 240);
        var weapons = new RegulationParam(original.Table("EquipParamWeapon"), "EQUIP_PARAM_WEAPON_ST", 664);
        var init = characters.Row(23342).ToArray();
        var weapon = weapons.Row(profile.WeaponId);
        var originalWeapon = weapons.Row(14140000);
        Require(weapon.Slice(422, 2).SequenceEqual(originalWeapon.Slice(422, 2)) &&
                weapon.Slice(230, 2).SequenceEqual(originalWeapon.Slice(230, 2)),
            "This weapon does not match Nepheli's tested weapon class.");
        Require(weapon[242] <= init[197] && weapon[243] <= init[198] && weapon[244] <= init[199] &&
                weapon[245] <= init[200] && weapon[405] <= init[201],
            "The companion does not meet the requested weapon's stat requirements.");
        Require(!characters.Contains(CustomRow) && !faces.Contains(CustomRow) &&
                !buddy.Ids.Any(id => I32(buddy.Row(id), 84) == CustomRow) &&
                !characters.Ids.Any(id => I32(characters.Row(id), 164) == CustomRow),
            "A stock row already uses or references the alpha's custom ID.");

        var summon = buddy.Row(Nepheli).ToArray();
        Require(I32(buddy.Row(Jellyfish), 4) == 236000 && I32(summon, 8) == 100000020 &&
                I32(summon, 12) == 100000020 && I32(summon, 84) == 23342,
            "The stock Jellyfish/Nepheli mapping did not match.");
        Write32(summon, 4, 236000);
        Write32(summon, 84, CustomRow);
        Write32(init, 16, profile.WeaponId);
        Write32(init, 24, profile.WeaponId);
        Write32(init, 32, -1);
        Write32(init, 164, CustomRow);
        init[228] = profile.BodyType;
        var face = faces.Row(1).ToArray();
        foreach (var (key, value) in profile.Face)
            face[FaceMapping.ParamOffset(FaceMapping.Offsets[key])] = value;

        var replacements = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var checks = new List<Action<RegulationBinary>>();
        void Replace(string name, string type, int size, RegulationParam table, Dictionary<int, byte[]> rows)
        {
            replacements.Add(name, table.Write(rows));
            checks.Add(binder => table.Verify(new RegulationParam(binder.Table(name), type, size), rows));
        }

        // Keep the original reinforcement curve and isolate the adjustments from the real Nepheli Puppet.
        if (profile.HpPercent != 100 || profile.DamagePercent != 100)
        {
            var effects = new RegulationParam(original.Table("SpEffectParam"), "SP_EFFECT_PARAM_ST", 912);
            var tuned = new Dictionary<int, byte[]>();
            for (var level = 0; level <= 10; level++)
            {
                var reference = 40 + level * 4;
                var originalId = I32(summon, reference);
                var newId = EffectStart + level;
                Require(originalId == StockEffectStart + level && !effects.Contains(newId),
                    "Unexpected reinforcement effect or custom ID collision.");
                var effect = effects.Row(originalId).ToArray();
                Scale(effect, 16, profile.HpPercent);
                foreach (var field in DamageRates)
                    Scale(effect, field, profile.DamagePercent);
                tuned.Add(newId, effect);
                Write32(summon, reference, newId);
            }
            Replace("SpEffectParam", "SP_EFFECT_PARAM_ST", 912, effects, tuned);
        }

        Replace("BuddyParam", "BUDDY_PARAM_ST", 160, buddy, new() { [Jellyfish] = summon });
        Replace("CharaInitParam", "CHARACTER_INIT_PARAM", 320, characters, new() { [CustomRow] = init });
        Replace("FaceParam", "FACE_PARAM_ST", 240, faces, new() { [CustomRow] = face });
        var package = original.Pack(replacements);
        var reopened = RegulationBinary.Read(package);
        original.Verify(reopened, replacements);
        foreach (var check in checks)
            check(reopened);

        Directory.CreateDirectory(parent);
        var staging = Path.Combine(parent, ".spirit-staging-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            Directory.CreateDirectory(Path.Combine(staging, "mod"));
            File.WriteAllBytes(Path.Combine(staging, "mod", "regulation.bin"), package);
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
        finally
        {
            if (Directory.Exists(staging))
                Directory.Delete(staging, recursive: true);
        }
    }

    private static void Scale(byte[] row, int offset, int percent)
    {
        if (percent == 100)
            return;
        var original = BinaryPrimitives.ReadSingleLittleEndian(row.AsSpan(offset));
        Require(float.IsFinite(original) && original > 0, "Unexpected reinforcement multiplier.");
        BinaryPrimitives.WriteSingleLittleEndian(row.AsSpan(offset), original * (percent / 100f));
    }
}
