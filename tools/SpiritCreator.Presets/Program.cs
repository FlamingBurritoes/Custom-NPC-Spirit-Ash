using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using SpiritCreator;
using static SpiritCreator.RegulationBinary;

if (args.Length is not (5 or 6) || args.Length == 6 && args[5] != "--check")
{
    Console.Error.WriteLine("Usage: SpiritCreator.Presets <stock regulation.bin> <WitchyBND PARAM XML directory> <entity list text/html> <model approximations.tsv> <saves directory> [--check]");
    return 2;
}

var regulation = File.ReadAllBytes(args[0]);
Require(regulation.Length == RegulationExporter.StockSize &&
    Convert.ToHexString(SHA256.HashData(regulation)) == RegulationExporter.StockHash, "Expected stock Steam 1.17.3 regulation.bin.");
var stock = Read(regulation);
RegulationParam Table(string name, string type, int size) => new(stock.Table(name), type, size);
var init = Table("CharaInitParam", "CHARACTER_INIT_PARAM", 320);
var faces = Table("FaceParam", "FACE_PARAM_ST", 240);
var npcs = Table("NpcParam", "NPC_PARAM_ST", 736);
var buddies = Table("BuddyParam", "BUDDY_PARAM_ST", 160);
var weapons = Table("EquipParamWeapon", "EQUIP_PARAM_WEAPON_ST", 664);
var curves = Table("ReinforceParamWeapon", "REINFORCE_PARAM_WEAPON_ST", 128);
var customs = Table("EquipParamCustomWeapon", "EQUIP_PARAM_CUSTOM_WEAPON_ST", 16);
var gems = Table("EquipParamGem", "EQUIP_PARAM_GEM_ST", 96);
var skills = Table("SwordArtsParam", "SWORD_ARTS_PARAM_ST", 32);
var names = ReadNames("CharaInitParam");
var spellNames = ReadNames("Magic");
var outDir = Path.GetFullPath(args[4]);
var check = args.Length == 6;
var index = new List<string> { "file\tsource\tcategories\tadaptations" };
var expected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

// The map list annotates roles; stock CharaInitParam and BuddyParam determine
// the build data. Multiple map entities may share the same loadout, so group by initializer.
var maps = new Dictionary<int, HashSet<byte>>();
foreach (var line in File.ReadLines(args[2]))
{
    var m = Regex.Match(line, @"\bc0000_\d+\b.*?\bnpc (\d+).*?\bchara (\d+)\b");
    if (!m.Success)
        continue;
    var npc = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
    var id = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
    if (!init.Contains(id))
        continue;
    if (!maps.TryGetValue(id, out var teams))
        maps[id] = teams = [];
    if (npcs.Contains(npc))
        teams.Add(npcs.Row(npc)[307]);
}
Require(maps.Count > 150, "The entity list did not contain the expected c0000 map references.");

var ashByInit = new Dictionary<int, List<(AshTargetChoice Ash, AshSpawnChoice Spawn)>>();
foreach (var ash in AshTargetCatalog.Options)
foreach (var spawn in ash.Rows)
{
    var id = I32(buddies.Row(spawn.RowId), 84);
    if (id < 0)
        continue;
    if (!ashByInit.TryGetValue(id, out var list))
        ashByInit[id] = list = [];
    list.Add((ash, spawn));
}

var declared = names.Where(kv => (kv.Key is >= 23000 and < 30000 || kv.Key is >= 2024000 and < 2029000) && kv.Value != "Human")
    .Select(kv => kv.Key);
var ids = maps.Keys.Union(ashByInit.Keys).Union(declared).Order().ToArray();
foreach (var id in ids)
{
    var title = names.GetValueOrDefault(id) ?? $"Humanoid {id}";
    if (id == 23252)
        title = "Sorcerer Rogier (Margit Co-op and Fia's Champions)";
    var teams = maps.GetValueOrDefault(id) ?? [];
    var roles = new List<string>();
    if (teams.Contains(2) || teams.Contains(33) || Regex.IsMatch(title, @"Co-op|Helpful Spirit", RegexOptions.IgnoreCase))
        roles.Add("Cooperator");
    if (teams.Contains(27) || teams.Contains(28) || Regex.IsMatch(title,
        @"Invad|Invasion|Recusant|Bloody Finger|Festering Fingerprint|Revenger", RegexOptions.IgnoreCase))
        roles.Add("Invader");
    if (ashByInit.TryGetValue(id, out var targets))
    {
        foreach (var (ash, spawn) in targets)
        {
            var label = ash.Rows.Count > 1 ? $"{ash.Name} - {spawn.Name}" : ash.Name;
            Generate(id, label, "Spirit Ashes", ["Spirit Ash", .. roles], ash.GoodsId, spawn.RowId);
        }
        // Some ashes share a character row with an invader, NPC or cooperator.
        if (!maps.ContainsKey(id))
            continue;
    }
    var group = roles.Contains("Cooperator") ? "Cooperators" : roles.Contains("Invader") ? "Invaders" : "NPCs";
    if (id is 24020 or 26050)
    {
        Generate(id, title, "Spirit Ashes", ["Spirit Ash", "Mimic reference"], null, null);
        continue;
    }
    if (id != 20000) // Generic developer template, not a shipped character preset.
        Generate(id, title, group, [.. roles, "NPC"], null, null);
}

foreach (var line in File.ReadLines(args[3]))
{
    if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
        continue;
    var col = line.Split('\t');
    Require(col.Length == 10, "Expected ten columns in the model approximation manifest.");
    var ashId = int.Parse(col[0], CultureInfo.InvariantCulture);
    var ash = ashId < 0 ? null : AshTargetCatalog.Options.Single(a => a.GoodsId == ashId);
    var selectedRow = int.Parse(col[2], CultureInfo.InvariantCulture);
    var spawn = ash is null ? null : selectedRow < 0 ? ash.Rows[0] : ash.Rows.Single(r => r.RowId == selectedRow);
    var title = col[1];
    var armor = new ArmorLoadout(Armor(col[5], ArmorSlot.Head), Armor(col[6], ArmorSlot.Chest),
        Armor(col[7], ArmorSlot.Hands), Armor(col[8], ArmorSlot.Legs));
    var spells = col[9] == "-" ? [] : col[9].Split('|').Select(name => SpellCatalog.All.Single(s => s.Name == name).Id).ToArray();
    var draft = SpiritProfile.CreateDefault() with
    {
        Character = ShortName(title, 16), SummonName = ShortName(title, 40),
        WeaponId = WeaponId(col[3]), LeftWeaponId = WeaponId(col[4]), Armor = armor, SpellIds = spells,
        BodyType = title is "Melina" or "Ranni the Witch" or "Malenia" or "Latenna the Albinauric" or "Black Knife Tiche"
            or "Nightmaiden Puppet" or "Swordstress Puppet" or "Cleanrot Knight Finlay" or "Perfumer Tricia"
            or "Curseblade Meera" or "Fire Knight Hilde" ? (byte)0 : (byte)1,
        TargetAshId = ash?.GoodsId, TargetBuddyRowId = spawn?.RowId,
        Shade = ash is null ? CompanionShade.None : CompanionShade.SpiritAsh
    };
    var notes = new List<string> { "Uses a separate stock character model; this is a humanoid loadout inspired by it, not the original model or moveset." };
    var sourceRole = title is "Melina" or "Blaidd" ? "Cooperator" : title == "Baleful Shadow" ? "Invader" : "NPC";
    WriteBuild(draft with { Attributes = FitStats(draft.Attributes!, draft, notes) }, [null, null, null],
        title, ash is null ? "Model references" : "Spirit Ashes",
        $"model reference {(spawn is null ? "" : $"BuddyParam {spawn.RowId}")}".Trim(),
        ["Inspired", ash is null ? sourceRole : "Spirit Ash"], notes);
}

if (check)
{
    Require(File.ReadAllLines(Path.Combine(outDir, "index.tsv")).SequenceEqual(index), "The preset index is out of date.");
    Require(Directory.EnumerateFiles(outDir, "*.json", SearchOption.AllDirectories)
        .Select(path => Path.GetRelativePath(outDir, path).Replace('\\', '/')).ToHashSet(StringComparer.OrdinalIgnoreCase)
        .SetEquals(expected), "Preset folder has missing or extra JSON files.");
}
else
    File.WriteAllLines(Path.Combine(outDir, "index.tsv"), index);
Console.WriteLine($"{(check ? "Verified" : "Generated")} {expected.Count} builds from {maps.Count} map-linked humanoid initializers plus stock NPC and ash variants.");
return 0;

void Generate(int id, string title, string group, string[] roles, int? ash, int? spawn)
{
    var row = init.Row(id);
    var notes = new List<string>();
    var right = Hand(id, row, [16, 20, 264], [232, 233, 234], "right", notes);
    var left = Hand(id, row, [24, 28, 268], [235, 236, 237], "left", notes);
    var spellIds = new List<int>();
    for (var slot = 0; slot < SpellCatalog.SlotCount; slot++)
    {
        var sid = I32(row, 96 + slot * 4);
        if (sid < 0)
            continue;
        var mapped = MapSpell(sid);
        if (mapped is null)
            notes.Add($"NPC spell {sid} ({spellNames.GetValueOrDefault(sid, "unlabelled")}) omitted.");
        else if (!spellIds.Contains(mapped.Value))
            spellIds.Add(mapped.Value);
    }
    if (EquipmentCatalog.Weapon(left.Id).Kind == WeaponKind.Catalyst)
    {
        if (right.Id >= 0 && !spellIds.Any(sid => CanCast(left.Id, sid)))
        {
            left = EmptyHand();
            notes.Add("Omitted unsupported left-hand catalyst; preserved the primary weapon.");
        }
        else if (right.Id >= 0 && EquipmentCatalog.Weapon(right.Id).Kind == WeaponKind.Catalyst)
        {
            if (spellIds.Count(sid => CanCast(left.Id, sid)) > spellIds.Count(sid => CanCast(right.Id, sid)))
                right = left;
            left = EmptyHand();
            notes.Add("Kept one catalyst; the editor cannot equip a left-hand catalyst.");
        }
        else
        {
            (right, left) = (left, right);
            notes.Add("Swapped hands to put the catalyst in the right hand.");
        }
    }
    var compatible = spellIds.Where(sid => CanCast(right.Id, sid)).ToArray();
    if (compatible.Length != spellIds.Count)
        notes.Add("Omitted spells the retained weapon cannot cast.");
    var armor = Enumerable.Range(0, 4).Select(i => I32(init.Row(id), 32 + i * 4)).ToArray();
    for (var slot = 0; slot < armor.Length; slot++)
        if (!EquipmentCatalog.ArmorFor((ArmorSlot)slot).Any(choice => choice.Id == armor[slot]))
        {
            notes.Add($"Unsupported {(ArmorSlot)slot} armor {armor[slot]} omitted.");
            armor[slot] = -1;
        }
    var talismans = Enumerable.Range(0, 4).Select(i => I32(init.Row(id), 64 + i * 4)).ToArray();
    var seen = new HashSet<int>();
    for (var i = 0; i < talismans.Length; i++)
        if (talismans[i] >= 0 && (!ExtrasCatalog.Talismans.Any(t => t.Id == talismans[i]) || !seen.Add(talismans[i])))
        {
            notes.Add($"Unsupported or repeated talisman {talismans[i]} omitted.");
            talismans[i] = -1;
        }
    var quick = new List<ConsumableStack>();
    for (var slot = 0; slot < 10; slot++)
    {
        var item = I32(row, 124 + 4 * slot);
        if (item < 0 || row[204 + slot] == 0)
            continue;
        var choice = ExtrasCatalog.Consumables.FirstOrDefault(c => c.Id == item && c.Id >= 0);
        if (choice is null || quick.Count == ExtrasCatalog.ConsumableSlots || quick.Any(c => c.Id == item))
            notes.Add($"Quick item {item} omitted (unsupported, duplicate, or no slot).");
        else
        {
            var count = Math.Min(row[204 + slot], choice.MaxQuantity);
            quick.Add(new(item, count));
            if (count != row[204 + slot])
                notes.Add($"{choice.Name} quantity capped at {count}.");
        }
    }
    var sourceStats = row.Slice(194, 8).ToArray();
    var stats = sourceStats.Select(v => Math.Clamp((int)v, NpcAttributes.Minimum, NpcAttributes.Maximum)).ToArray();
    if (!sourceStats.Select(v => (int)v).SequenceEqual(stats))
        notes.Add("Starting attributes clamped to the editor range.");
    var face = I32(row, 164);
    if (!faces.Contains(face))
        notes.Add($"Missing stock face {face}; uses the default face for this body type.");
    if (id is 24020 or 26050)
        notes.Add("Mimic Tear's live copy behavior is not supported. This is its stock initializer as a fixed build.");
    var profile = SpiritProfile.CreateDefault() with
    {
        Character = ShortName(title, 16), SummonName = ShortName(title, 40),
        BodyType = row[228] is 0 or 1 ? row[228] : (byte)1, StockFaceId = faces.Contains(face) ? face : null,
        WeaponId = right.Id, LeftWeaponId = left.Id, RightAffinity = right.Affinity, LeftAffinity = left.Affinity,
        RightReinforcement = right.Level, LeftReinforcement = left.Level,
        RightAshOfWarId = right.Gem, LeftAshOfWarId = left.Gem,
        RightAmmoId = Ammo(id, right.Id, false), LeftAmmoId = Ammo(id, left.Id,
            AmmunitionCatalog.TypeForWeapon(right.Id) == AmmunitionCatalog.TypeForWeapon(left.Id)),
        Armor = new(armor[0], armor[1], armor[2], armor[3]),
        Attributes = new(stats[0], stats[1], stats[2], stats[3], stats[4], stats[5], stats[6], stats[7]),
        SpellIds = compatible, TalismanIds = talismans, TargetAshId = ash, TargetBuddyRowId = spawn,
        Shade = ash is < 2_000_000 ? CompanionShade.Puppet : ash is not null ? CompanionShade.SpiritAsh :
            group == "Cooperators" ? CompanionShade.Cooperator : group == "Invaders" ? CompanionShade.Invader : CompanionShade.None
    };
    if (compatible.Length > 0 && left.Id < 0)
        profile = profile with { CombatPreset = compatible.Any(s => SpellCatalog.All.Single(p => p.Id == s).CastType == 0)
            ? CombatPreset.Sorcerer : CombatPreset.Caster };
    WriteBuild(profile with { Attributes = FitStats(profile.Attributes!, profile, notes) },
        Enumerable.Range(0, ExtrasCatalog.ConsumableSlots).Select(i => i < quick.Count ? quick[i] : null).ToArray(),
        title, group, $"CharaInitParam {id}" + (spawn is { } buddy ? $"; BuddyParam {buddy}" : ""), roles, notes,
        spawn is { } b ? $"{id} - {b}" : id.ToString(CultureInfo.InvariantCulture));
}

int? MapSpell(int id)
{
    if (SpellCatalog.All.Any(spell => spell.Id == id))
        return id;
    if (!spellNames.TryGetValue(id, out var name))
        return null;
    name = Regex.Replace(name, @"^\[[^\]]+\]\s*", "");
    var key = Normalize(name);
    var exact = SpellCatalog.All.FirstOrDefault(spell => Normalize(spell.Name) == key);
    if (exact is not null)
        return exact.Id;
    key = Regex.Replace(key, @"\d+$", "");
    return SpellCatalog.All.FirstOrDefault(spell => Normalize(spell.Name) == key)?.Id;
}

bool CanCast(int weapon, int spell) => SpellCatalog.ForWeapon(weapon).Any(choice => choice.Id == spell);

(int Id, WeaponAffinity Affinity, int Level, int? Gem) Hand(int id, ReadOnlySpan<byte> row, int[] offsets, int[] kinds, string hand, List<string> notes)
{
    for (var slot = 0; slot < offsets.Length; slot++)
    {
        var value = I32(row, offsets[slot]);
        if (value < 0)
            continue;
        var equipped = value;
        int? gem = null;
        var customLevel = -1;
        if (row[kinds[slot]] == 1)
        {
            if (!customs.Contains(value))
            {
                notes.Add($"{hand} custom weapon {value} missing.");
                continue;
            }
            var custom = customs.Row(value);
            equipped = I32(custom, 0);
            gem = I32(custom, 4);
            customLevel = custom[8];
        }
        var rowId = equipped / 100 * 100; // Reinforcement is the last two decimal digits, not part of the PARAM row ID.
        var baseId = equipped / 10000 * 10000;
        var weapon = EquipmentCatalog.Weapons.FirstOrDefault(w => w.Id == baseId);
        var direct = weapon is not null && weapons.Contains(rowId) &&
            (rowId == baseId || I32(weapons.Row(rowId), 96) == baseId &&
                U16(weapons.Row(rowId), 188) == U16(weapons.Row(baseId), 188));
        if (!direct)
        {
            weapon = null;
            if (weapons.Contains(rowId))
            {
                var source = weapons.Row(rowId);
                var model = U16(source, 188);
                var type = U16(source, 422);
                var origin = I32(source, 96);
                if (model > 0)
                    weapon = EquipmentCatalog.Weapons.Where(w => w.Id >= 0 && U16(weapons.Row(w.Id), 188) == model)
                        .OrderByDescending(w => w.Id == origin).ThenByDescending(w => w.Type == type).FirstOrDefault();
            }
            if (weapon is not null)
                notes.Add($"{hand} NPC-only weapon {equipped} mapped to {weapon.Name} (same model).");
        }
        if (weapon is null || weapon.Id < 0)
        {
            notes.Add($"Unsupported {hand} weapon {equipped} omitted.");
            continue;
        }
        if (slot > 0)
            notes.Add($"Promoted alternate {hand} weapon to the primary slot.");
        var affinityValue = direct ? (equipped - baseId) / 100 : 0;
        var affinity = (WeaponAffinity)affinityValue;
        var level = Math.Clamp(customLevel >= 0 ? customLevel : equipped % 100, 0, weapon.MaxLevel);
        var available = AshOfWarCatalog.ForWeapon(weapon.Id);
        if (gem is >= 0 && !available.Any(a => a.GemId == gem))
        {
            var skill = gems.Contains(gem.Value) ? I32(gems.Row(gem.Value), 24) : -1;
            var mapped = available.FirstOrDefault(a => a.GemId >= 0 &&
                AshOfWarCatalog.Resolve(weapon.Id, a.GemId).SkillId == skill);
            notes.Add(mapped is null ? $"Unsupported {hand} Ash of War {gem}; kept stock skill."
                : $"{hand} NPC Ash of War {gem} mapped to {mapped.Name}.");
            gem = mapped?.GemId;
        }
        if (gem is >= 0)
        {
            var entry = AshOfWarCatalog.Resolve(weapon.Id, gem.Value);
            if (!skills.Contains(entry.SkillId))
                gem = null;
        }
        if (gem is < 0)
            gem = null;
        if (!WeaponUpgradeCatalog.ForWeapon(weapon.Id, gem).Any(option => option.Affinity == affinity))
        {
            notes.Add($"Unsupported {hand} affinity {affinityValue}; set to Standard.");
            affinity = WeaponAffinity.Standard;
        }
        while (true)
        {
            try { RegulationExporter.CheckWeapon(weapons, curves, weapon.Id, affinity, level, new(99, 99, 99, 99, 99, 99, 99, 99), hand); break; }
            catch (InvalidDataException) when (level > 0) { level--; }
        }
        if (level != (customLevel >= 0 ? customLevel : equipped % 100))
            notes.Add($"{hand} reinforcement adjusted to +{level}.");
        if (offsets.Skip(slot + 1).Any(offset => I32(init.Row(id), offset) >= 0))
            notes.Add($"Additional {hand} weapon slots omitted.");
        return (weapon.Id, affinity, level, gem);
    }
    return EmptyHand();
}

static (int Id, WeaponAffinity Affinity, int Level, int? Gem) EmptyHand() => (-1, WeaponAffinity.Standard, 0, null);

int? Ammo(int id, int weapon, bool secondary)
{
    var type = AmmunitionCatalog.TypeForWeapon(weapon);
    if (type == 0)
        return null;
    var row = init.Row(id);
    var offset = type is 85 or 86 ? 52 : 48;
    var candidate = I32(row, offset + (secondary ? 8 : 0));
    if (AmmunitionCatalog.All.Any(a => a.Type == type && a.Id == candidate))
        return candidate;
    return AmmunitionCatalog.Default(type)?.Id;
}

NpcAttributes FitStats(NpcAttributes stats, SpiritProfile profile, List<string> notes)
{
    var requirements = new[] { EquipmentCatalog.Weapon(profile.WeaponId).Requirements,
            EquipmentCatalog.Weapon(profile.LeftWeaponId!.Value).Requirements }
        .Concat((profile.SpellIds ?? []).Select(sid => SpellCatalog.All.Single(s => s.Id == sid).Requirements)).ToArray();
    var fitted = stats with
    {
        Strength = Math.Max(stats.Strength, requirements.Max(r => r.Strength)),
        Dexterity = Math.Max(stats.Dexterity, requirements.Max(r => r.Dexterity)),
        Intelligence = Math.Max(stats.Intelligence, requirements.Max(r => r.Intelligence)),
        Faith = Math.Max(stats.Faith, requirements.Max(r => r.Faith)),
        Arcane = Math.Max(stats.Arcane, requirements.Max(r => r.Arcane))
    };
    if (fitted != stats)
        notes.Add("Attributes raised to meet the editor’s one-handed gear and spell requirements.");
    return fitted;
}

void WriteBuild(SpiritProfile profile, ConsumableStack?[] quick, string name, string category, string source,
    string[] roles, List<string> notes, string? suffix = null)
{
    var file = $"{category}/{SafeName(name)}{(suffix is null ? "" : $" [{suffix}]")}.json";
    Require(expected.Add(file), $"Duplicate preset {file}.");
    var path = Path.Combine(outDir, file);
    var build = new SpiritBuild { Profile = profile, QuickItems = quick };
    var export = build.ToExportProfile();
    export.Validate();
    // Also check the stock variant and requirements for both hands, without writing a mod.
    RegulationExporter.CheckWeapon(weapons, curves, profile.WeaponId, profile.RightAffinity, profile.RightReinforcement, profile.Attributes!, "right");
    RegulationExporter.CheckWeapon(weapons, curves, profile.LeftWeaponId!.Value, profile.LeftAffinity, profile.LeftReinforcement, profile.Attributes!, "left");
    if (check)
    {
        var actual = SpiritBuild.Load(path);
        Require(System.Text.Json.JsonSerializer.Serialize(actual, SpiritProfile.JsonOptions) ==
            System.Text.Json.JsonSerializer.Serialize(build, SpiritProfile.JsonOptions), $"Preset drift: {file}.");
    }
    else
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        build.Save(path);
    }
    index.Add($"{file}\t{source}\t{string.Join(", ", roles)}\t{string.Join(" ", notes.Distinct())}");
}

Dictionary<int, string> ReadNames(string table) => XDocument.Load(Path.Combine(args[1], table + ".param.xml"))
    .Descendants("row").Where(row => !string.IsNullOrWhiteSpace((string?)row.Attribute("paramdexName")))
    .ToDictionary(row => (int)row.Attribute("id")!, row => Clean((string)row.Attribute("paramdexName")!));

static string Normalize(string text) => Regex.Replace(text.ToLowerInvariant(), "[^a-z0-9]", "");
static string Clean(string text) => text.Replace("Varr�", "Varré").Replace("Jolan", "Jolán").Replace("Jol�n", "Jolán").Trim();
static string ShortName(string text, int max)
{
    text = Regex.Replace(text, @"\s+\(.*$", "").Trim();
    return text.Length <= max ? text : text[..max].TrimEnd();
}
static string SafeName(string text) => Regex.Replace(Clean(text), "[<>:\"/\\\\|?*]|[\\x00-\\x1F]", "-").Trim().TrimEnd('.');
static int WeaponId(string name) => name == "-" ? -1 : EquipmentCatalog.Weapons.Single(w => w.Name == name).Id;
static int Armor(string name, ArmorSlot slot) => name == "-" ? -1 : EquipmentCatalog.ArmorFor(slot).Single(w => w.Name == name).Id;
static int U16(ReadOnlySpan<byte> row, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(row[offset..]);
