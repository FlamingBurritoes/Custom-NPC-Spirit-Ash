using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SpiritCreator;

const int slotStart = 0x300;
const int slotSize = 0x280010;
const int global = 0x19003A0;
const int summary = global + 0x1964;
const int profileOffset = summary + 10;

var bytes = new byte[0x1BA03D0];
var data = slotStart + 16;
var player = data + 0x20 + 5120 * 8 + 13; // One weapon record instead of an empty record.
var projectiles = player + 0x94CC;
var face = projectiles + 4 + 8 + 0x9C + 0xC;
"BND4"u8.CopyTo(bytes);
Write(0x0C, 12);
Write(0x10, 0x40);
Write(0x20, 0x20);
Write(0x28, slotStart);
Write(0x48, slotSize);
Write(0x50, slotStart);
Write(0x188, 0x60010);
Write(0x190, global);
Write(data, 211);
Write(data + 0x20, 0x80800000);
Write(projectiles, 1);
Write(face, uint.MaxValue);
"FACE"u8.CopyTo(bytes.AsSpan(face + 4));
Write(face + 8, 4);
Write(face + 12, 0x120);
bytes[face + 0x14] = 9;
bytes[face + 0x35] = 201;
bytes[face + 0xB4] = 173;
bytes[face + 0x124] = 1;
Write(face + 0x127, uint.MaxValue);
Write(face + 0x12B, uint.MaxValue);
bytes[summary] = 1;
bytes[player + 0xB6] = bytes[profileOffset + 0x242] = 1;
Encoding.Unicode.GetBytes("Fixture").CopyTo(bytes, player + 0x94);
Encoding.Unicode.GetBytes("Fixture").CopyTo(bytes, profileOffset);
bytes.AsSpan(face, 0x124).CopyTo(bytes.AsSpan(profileOffset + 0x36));
HashEntry(slotStart, slotSize);
HashEntry(global, 0x60010);

var originalHash = SHA256.HashData(bytes);
var slots = SaveAppearance.ListCharacters(bytes);
Check(slots.SequenceEqual([new CharacterSlot(1, "Fixture")]), "active slot list");
var profile = SaveAppearance.Import(bytes, "fixture");
Check(profile is { Character: "Fixture", BodyType: 1, WeaponId: EquipmentCatalog.Club,
        LeftWeaponId: EquipmentCatalog.Empty, Attributes.Level: 1 }, "slot identification and Wretch defaults");
Check(new NpcAttributes(Strength: 14, Dexterity: 12).Level == 7, "computed rune level");
Check(EquipmentCatalog.Weapon(SpiritProfile.JawboneAxe).Requirements.Describe(new NpcAttributes()) == "STR 14",
    "missing one-handed requirement");
Check(EquipmentCatalog.Weapon(SpiritProfile.JawboneAxe).Requirements.Describe(new NpcAttributes(Strength: 14)) == "",
    "met one-handed requirements");
Reject(() => (profile with { Attributes = new(Vigor: 9) }).Validate(), "minimum attribute");
Reject(() => (profile with { Armor = new(Head: 1500100) }).Validate(), "armor in wrong slot");
(profile with { Armor = new(Head: 1500000, Chest: 1500100, Hands: 1500200, Legs: 1500300) }).Validate();
Check(AshOfWarCatalog.ForWeapon(EquipmentCatalog.Empty).SequenceEqual([AshOfWarCatalog.Default]), "empty hand Ash filter");
Check(AshOfWarCatalog.ForWeapon(2_000_000).Any(ash => ash.GemId == 11500) &&
      !AshOfWarCatalog.ForWeapon(EquipmentCatalog.Club).Any(ash => ash.GemId == 11500), "Square Off is straight-sword only");
Check(AshOfWarCatalog.ForWeapon(31_330_000).Any(ash => ash.GemId == 30900), "No Skill on Heater Shield");
Reject(() => (profile with { RightAshOfWarId = 11500 }).Validate(), "incompatible right-hand Ash");
Reject(() => (profile with { LeftAshOfWarId = 50200 }).Validate(), "Ash on empty hand");
var upgraded = profile with
{
    RightAffinity = WeaponAffinity.Cold, RightReinforcement = 25, RightAshOfWarId = 50200,
    LeftWeaponId = EquipmentCatalog.HeaterShield, LeftAffinity = WeaponAffinity.Magic,
    LeftReinforcement = 8, LeftAshOfWarId = 30900
};
upgraded.Validate();
Check(JsonSerializer.Deserialize<SpiritProfile>(JsonSerializer.Serialize(upgraded, SpiritProfile.JsonOptions),
    SpiritProfile.JsonOptions) is
    { RightAffinity: WeaponAffinity.Cold, RightReinforcement: 25, LeftAffinity: WeaponAffinity.Magic, LeftReinforcement: 8 },
    "independent weapon upgrades survive JSON round-trip");
Check(WeaponUpgradeCatalog.ForWeapon(2_000_000, 20100).Select(choice => choice.Affinity).SequenceEqual(
    [WeaponAffinity.Standard, WeaponAffinity.Heavy, WeaponAffinity.Keen, WeaponAffinity.Quality,
        WeaponAffinity.Lightning, WeaponAffinity.Sacred]), "Sacred Blade filters affinity options");
Check(WeaponUpgradeCatalog.Reinforcements(EquipmentCatalog.Weapon(14_140_000)).Last().Level == 10 &&
    WeaponUpgradeCatalog.Reinforcements(EquipmentCatalog.Weapon(EquipmentCatalog.GlintstoneStaff)).Last().Level == 25 &&
    WeaponUpgradeCatalog.Reinforcements(EquipmentCatalog.Weapon(EquipmentCatalog.Empty)).Count == 1,
    "Somber, regular catalyst and empty reinforcement ranges");
Reject(() => (upgraded with { WeaponId = 2_000_000, RightAshOfWarId = 20100 }).Validate(), "incompatible Ash affinity");
Reject(() => (upgraded with { RightReinforcement = 26 }).Validate(), "regular above +25");
Reject(() => (profile with { WeaponId = 14_140_000, RightReinforcement = 11 }).Validate(), "Somber above +10");
Reject(() => (profile with { WeaponId = 14_140_000, RightAffinity = WeaponAffinity.Heavy }).Validate(), "Somber affinity");
Reject(() => (profile with { WeaponId = 14_140_000, RightAshOfWarId = 50200 }).Validate(), "Somber Ash");
Reject(() => (profile with { WeaponId = EquipmentCatalog.FingerSeal, RightAffinity = WeaponAffinity.Sacred }).Validate(), "catalyst affinity");
Reject(() => (profile with { LeftReinforcement = 1 }).Validate(), "reinforced empty hand");
Reject(() => (profile with { RightAffinity = (WeaponAffinity)32 }).Validate(), "invalid affinity enum");
var withAshes = profile with { RightAshOfWarId = 50200, LeftWeaponId = 31_330_000, LeftAshOfWarId = 30900 };
withAshes.Validate();
Check(profile.Shade == CompanionShade.None && ShadeChoice.Options[0].Shade == CompanionShade.None,
    "no shade is the default");
foreach (var choice in ShadeChoice.Options)
{
    var shaded = profile with { Shade = choice.Shade };
    shaded.Validate();
    var serialized = JsonSerializer.Serialize(shaded, SpiritProfile.JsonOptions);
    Check(JsonSerializer.Deserialize<SpiritProfile>(serialized, SpiritProfile.JsonOptions)?.Shade == choice.Shade,
        $"round-trip {choice.Shade} shade");
}
Reject(() => (profile with { Shade = (CompanionShade)99 }).Validate(), "unknown shade");
Check(CombatPresetChoice.Options[0] is { Preset: CombatPreset.Default, StarterRight: null }, "Mimic-style default leaves gear alone");
foreach (var choice in CombatPresetChoice.Options)
{
    var setup = profile with
    {
        CombatPreset = choice.Preset,
        WeaponId = choice.StarterRight ?? profile.WeaponId,
        LeftWeaponId = choice.StarterLeft
    };
    setup.Validate();
    var serialized = JsonSerializer.Serialize(setup, SpiritProfile.JsonOptions);
    Check(JsonSerializer.Deserialize<SpiritProfile>(serialized, SpiritProfile.JsonOptions)?.CombatPreset == choice.Preset,
        $"round-trip {choice.Preset} preset");
}
foreach (var catalyst in new[] { EquipmentCatalog.FingerSeal, EquipmentCatalog.GlintstoneStaff })
{
    Check(WeaponUpgradeCatalog.ForWeapon(catalyst, null).Count == 1, "no catalyst infusions");
    Reject(() => (profile with { LeftWeaponId = catalyst }).Validate(), "left-hand catalyst");
    Reject(() => (profile with { CombatPreset = CombatPreset.Warrior, WeaponId = catalyst }).Validate(), "Warrior catalyst");
}
Reject(() => (profile with { CombatPreset = CombatPreset.Warrior, WeaponId = EquipmentCatalog.HeaterShield }).Validate(),
    "Warrior without a melee weapon");
Reject(() => (profile with { CombatPreset = CombatPreset.Caster }).Validate(), "Caster without seal");
Reject(() => (profile with { CombatPreset = CombatPreset.Sorcerer, WeaponId = EquipmentCatalog.FingerSeal }).Validate(),
    "Sorcerer with wrong catalyst");
Reject(() => (profile with
{
    CombatPreset = CombatPreset.Caster, WeaponId = EquipmentCatalog.FingerSeal, LeftWeaponId = EquipmentCatalog.HeaterShield
}).Validate(), "Caster with an occupied left hand");
Reject(() => (profile with { CombatPreset = (CombatPreset)99 }).Validate(), "unknown combat preset");
var customCaster = profile with
{
    CombatPreset = CombatPreset.Caster, WeaponId = EquipmentCatalog.FingerSeal, Attributes = new(Faith: 30, Arcane: 12),
    SpellIds = [6900, 6000, 6210, 6422, 6600, 7030, 2007800]
};
customCaster.Validate();
Check(JsonSerializer.Deserialize<SpiritProfile>(JsonSerializer.Serialize(customCaster, SpiritProfile.JsonOptions),
        SpiritProfile.JsonOptions)?.SpellIds?.SequenceEqual(customCaster.SpellIds) == true, "custom spells survive JSON round-trip");
Reject(() => (customCaster with { SpellIds = [.. customCaster.SpellIds, 6010] }).Validate(), "eighth spell");
Reject(() => (customCaster with { SpellIds = [6900, 6900] }).Validate(), "duplicate spell");
Reject(() => (customCaster with { SpellIds = [4000] }).Validate(), "sorcery on a seal");
Reject(() => (customCaster with { SpellIds = [42] }).Validate(), "unknown spell");
Reject(() => (customCaster with { SpellIds = [] }).Validate(), "Caster with no spells");
Reject(() => (customCaster with { Attributes = new(Faith: 30), SpellIds = [7030] }).Validate(), "missing spell ARC");
Reject(() => (customCaster with { Attributes = new(), SpellIds = [6900] }).Validate(), "missing spell FAI");
Reject(() => (profile with { SpellIds = [4000] }).Validate(), "spell without catalyst");
(customCaster with { CombatPreset = CombatPreset.Default, SpellIds = [] }).Validate();
var withExtras = profile with
{
    TalismanIds = [1000, 4003, -1, 1150],
    Consumables = [new(50201, 1), new(300, 2), new(820, 1)]
};
withExtras.Validate();
var extrasRoundTrip = JsonSerializer.Deserialize<SpiritProfile>(
    JsonSerializer.Serialize(withExtras, SpiritProfile.JsonOptions), SpiritProfile.JsonOptions);
Check(extrasRoundTrip?.TalismanIds?.SequenceEqual(withExtras.TalismanIds) == true &&
    extrasRoundTrip.Consumables?.SequenceEqual(withExtras.Consumables) == true, "extras survive JSON round-trip");
Reject(() => (withExtras with { TalismanIds = [1000, 1000] }).Validate(), "duplicate talisman");
Reject(() => (withExtras with { TalismanIds = [42] }).Validate(), "unknown talisman");
Reject(() => (withExtras with { TalismanIds = [1000, 4003, 1150, 1210, 2130] }).Validate(), "fifth talisman");
Reject(() => (withExtras with { Consumables = [new(1001, 1)] }).Validate(), "player flask instead of NPC flask");
Reject(() => (withExtras with { Consumables = [new(50201, 4)] }).Validate(), "NPC flask above cap");
Reject(() => (withExtras with { Consumables = [new(300, 0)] }).Validate(), "zero item quantity");
Reject(() => (withExtras with { Consumables = [new(300, 1), new(300, 2)] }).Validate(), "duplicate item type");
Reject(() => (withExtras with { Consumables = [new(300, 1), new(820, 1), new(50201, 1), new(1730, 1)] }).Validate(), "fourth item type");
Check(SpellCatalog.ForWeapon(EquipmentCatalog.GlintstoneStaff).Any(spell => spell.Id == 6500) &&
    SpellCatalog.ForWeapon(EquipmentCatalog.FingerSeal).Any(spell => spell.Id == 5040), "catalog based on cast type, not ID range");
Check(JsonSerializer.Deserialize<SpiritProfile>(
        JsonSerializer.Serialize(withAshes, SpiritProfile.JsonOptions), SpiritProfile.JsonOptions)
    is { RightAshOfWarId: 50200, LeftAshOfWarId: 30900 }, "Ash selections survive JSON round-trip");
Check(profile.Face.Count == 157 && profile.Face["hair_partsId"] == 9 &&
      profile.Face["faceGeoData01"] == 201 && profile.Face["chrBodyScaleRLeg"] == 173, "mapping");
Check(originalHash.SequenceEqual(SHA256.HashData(bytes)), "input remained unchanged");
var json = JsonSerializer.Serialize(profile, SpiritProfile.JsonOptions);
var roundTrip = JsonSerializer.Deserialize<SpiritProfile>(json, SpiritProfile.JsonOptions)!;
roundTrip.Validate();
Check(profile.Face.OrderBy(p => p.Key).SequenceEqual(roundTrip.Face.OrderBy(p => p.Key)), "JSON round-trip");
(roundTrip with { WeaponId = SpiritProfile.BattleAxe }).Validate();
Check(roundTrip is { HpPercent: 100, DamagePercent: 100 }, "neutral combat defaults");
var legacyJson = JsonSerializer.SerializeToNode(profile, SpiritProfile.JsonOptions)!.AsObject();
legacyJson.Remove("hpPercent");
legacyJson.Remove("damagePercent");
Check(legacyJson.Deserialize<SpiritProfile>(SpiritProfile.JsonOptions) is
    { HpPercent: 100, DamagePercent: 100, CombatPreset: null, SpellIds: null, TalismanIds: null, Consumables: null,
        RightAffinity: WeaponAffinity.Standard, LeftAffinity: WeaponAffinity.Standard, RightReinforcement: 0, LeftReinforcement: 0 },
    "old profile defaults");
(profile with { HpPercent = 200, DamagePercent = 50 }).Validate();
Reject(() => (profile with { HpPercent = 49 }).Validate(), "minimum HP");
Reject(() => (profile with { DamagePercent = 201 }).Validate(), "maximum damage");
Check(FaceMapping.Offsets.Values.Select(FaceMapping.ParamOffset).Distinct().Count() == 157,
    "unique destination appearance fields");
var dcxHeader = new byte[76];
BinaryPrimitives.WriteInt32BigEndian(dcxHeader.AsSpan(44), 0x20);
dcxHeader[48] = 21;
var dcx = RegulationBinary.CompressDcx(dcxHeader, new byte[160_000]);
var compressedSize = BinaryPrimitives.ReadInt32BigEndian(dcx.AsSpan(32));
Check(dcx.AsSpan(44, 5).SequenceEqual(new byte[] { 0, 0, 0, 0x20, 15 }), "DCX header length and level");
Check(dcx.AsSpan(76, 6).SequenceEqual(new byte[] { 0x28, 0xB5, 0x2F, 0xFD, 0, 0x30 }),
    "Zstandard frame without content size, 64 KiB window");
Check(compressedSize > 6 && dcx.Length == ((76 + compressedSize + 15) & ~15) &&
      dcx.AsSpan(76 + compressedSize).IndexOfAnyExcept((byte)0) == -1, "DCX zero alignment");
Reject(() => (profile with { WeaponId = 42 }).Validate(), "weapon ID");
Reject(() => (profile with { SchemaVersion = 2 }).Validate(), "schema");
Reject(() => SaveAppearance.Import(bytes, "missing"), "missing slot");
Reject(() => SaveAppearance.Import(bytes, new CharacterSlot(1, "Stale")), "stale slot");
Reject(() => SaveAppearance.Import(bytes.AsSpan(0, 100), "Fixture"), "short save");
bytes[summary + 1] = 1;
bytes.AsSpan(profileOffset, 0x24C).CopyTo(bytes.AsSpan(profileOffset + 0x24C));
HashEntry(global, 0x60010);
Check(SaveAppearance.ListCharacters(bytes).SequenceEqual(
    [new CharacterSlot(1, "Fixture"), new CharacterSlot(2, "Fixture")]), "duplicate names appear as distinct slots");
Reject(() => SaveAppearance.Import(bytes, "Fixture"), "ambiguous name");
Check(SaveAppearance.Import(bytes, slots[0]).Character == "Fixture", "explicit slot with duplicate name");
bytes[face + 0x14]++;
Reject(() => SaveAppearance.Import(bytes, "Fixture"), "slot checksum");
HashEntry(slotStart, slotSize);
Reject(() => SaveAppearance.Import(bytes, "Fixture"), "slot/summary appearance mismatch");
bytes[face + 0x14]--;
Write(face + 0x127, 81000);
HashEntry(slotStart, slotSize);
Reject(() => SaveAppearance.Import(bytes, "Fixture"), "unsupported quest override");
Console.WriteLine("Synthetic save import, mapping, JSON and validation checks passed.");
if (args is [var stockPath])
    VerifyCombatExports(stockPath);

void Write(int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
void HashEntry(int start, int size) =>
    MD5.HashData(bytes.AsSpan(start + 16, size - 16)).CopyTo(bytes, start);

static void Check(bool condition, string test)
{
    if (!condition)
        throw new Exception($"Failed {test}");
}

static void Reject(Action run, string test)
{
    try
    {
        run();
    }
    catch (InvalidDataException)
    {
        return;
    }
    throw new Exception($"Failed to reject {test}");
}

void VerifyCombatExports(string stockPath)
{
    var root = Path.Combine(Path.GetTempPath(), "SpiritCreator-preset-tests-" + Guid.NewGuid().ToString("N"));
    var stock = RegulationBinary.Read(File.ReadAllBytes(stockPath));
    var untouched = new[] { "Magic", "NpcThinkParam", "NpcAiBehaviorProbability", "EquipParamWeapon",
        "EquipParamAccessory", "EquipParamGoods", "ReinforceParamWeapon", "EquipParamGem" };
    var weapons = new RegulationParam(stock.Table("EquipParamWeapon"), "EQUIP_PARAM_WEAPON_ST", 664);
    var curves = new RegulationParam(stock.Table("ReinforceParamWeapon"), "REINFORCE_PARAM_WEAPON_ST", 128);
    var gems = new RegulationParam(stock.Table("EquipParamGem"), "EQUIP_PARAM_GEM_ST", 96);
    var maxStats = new NpcAttributes(Strength: 99, Dexterity: 99, Intelligence: 99, Faith: 99, Arcane: 99);
    foreach (var weapon in EquipmentCatalog.Weapons)
    {
        foreach (var affinity in WeaponUpgradeCatalog.ForWeapon(weapon.Id, null))
            foreach (var upgrade in WeaponUpgradeCatalog.Reinforcements(weapon))
                RegulationExporter.CheckWeapon(weapons, curves, weapon.Id, affinity.Affinity, upgrade.Level, maxStats, "Catalog");
        foreach (var choice in AshOfWarCatalog.ForWeapon(weapon.Id).Where(choice => choice.GemId >= 0))
        {
            var entry = AshOfWarCatalog.Resolve(weapon.Id, choice.GemId);
            Check((BinaryPrimitives.ReadUInt16LittleEndian(gems.Row(choice.GemId)[48..]) & 0x1FFF) ==
                entry.AffinityMask, "stock infusion mask");
        }
    }
    Console.WriteLine("Weapon affinities and reinforcement ranges verified against stock tables.");
    var magic = new RegulationParam(stock.Table("Magic"), "MAGIC_PARAM_ST", 168);
    foreach (var catalyst in new[] { EquipmentCatalog.FingerSeal, EquipmentCatalog.GlintstoneStaff })
        foreach (var spell in SpellCatalog.ForWeapon(catalyst))
            RegulationExporter.VerifySpell(magic.Row(spell.Id), spell, catalyst);
    Console.WriteLine("Spell catalog requirements, catalyst types and AI usage IDs verified.");
    Check(EquipmentCatalog.Weapons.Count > 450 && SpellCatalog.All.Count > 200 &&
        ExtrasCatalog.Talismans.Count > 150, "expanded stock catalogs");
    Check(SpellCatalog.ForWeapon(33_510_000).Select(spell => spell.CastType).Distinct().Count() == 2,
        "Staff of the Great Beyond supports both spell families");
    Reject(() => (profile with { WeaponId = 40_000_000, RightAmmoId = 52_000_000 }).Validate(),
        "crossbow bolt cannot equip on a bow");
    try
    {
        foreach (var (name, setup, bias, spells) in new (string, SpiritProfile, int, int[])[]
        {
            ("Legacy", profile with { Shade = CompanionShade.Puppet }, -1, []),
            ("Default starter spells", profile with
            {
                CombatPreset = CombatPreset.Default, WeaponId = EquipmentCatalog.FingerSeal, Shade = CompanionShade.Puppet
            }, -1, [6010, 6000]),
            ("Default no spells", profile with
            {
                CombatPreset = CombatPreset.Default, WeaponId = EquipmentCatalog.FingerSeal, SpellIds = [],
                Shade = CompanionShade.Puppet, TalismanIds = [], Consumables = []
            }, -1, []),
            ("Warrior", profile with
            {
                CombatPreset = CombatPreset.Warrior, LeftWeaponId = EquipmentCatalog.HeaterShield,
                RightAshOfWarId = 50200, Shade = CompanionShade.Puppet
            }, 18657, []),
            ("Both hands upgraded with Ashes", upgraded with { Shade = CompanionShade.Puppet }, -1, []),
            ("Bow and crossbow ammunition", profile with
            {
                WeaponId = 40_000_000, RightAmmoId = 50_040_000, LeftWeaponId = 43_000_000,
                Attributes = new(Strength: 20, Dexterity: 20), Shade = CompanionShade.Puppet
            }, -1, []),
            ("Somber and direct affinity", profile with
            {
                WeaponId = 14_140_000, RightReinforcement = 10, Attributes = new(Strength: 19, Dexterity: 15),
                LeftWeaponId = 2_000_000, LeftAffinity = WeaponAffinity.Keen, LeftReinforcement = 16,
                Shade = CompanionShade.Puppet
            }, -1, []),
            ("Caster", customCaster with
            {
                Shade = CompanionShade.Invader, TalismanIds = withExtras.TalismanIds, Consumables = withExtras.Consumables
            }, 18663, [6900, 6000, 6210, 6422, 6600, 7030, 2007800]),
            ("Sorcerer", profile with
            {
                CombatPreset = CombatPreset.Sorcerer, WeaponId = EquipmentCatalog.GlintstoneStaff,
                Attributes = new(Intelligence: 28), SpellIds = [2004310, 4440], RightReinforcement = 25
            }, 18667, [2004310, 4440])
        })
        {
            var output = Path.Combine(root, name);
            RegulationExporter.Export(setup with { HpPercent = 150, DamagePercent = 115 }, stockPath, output);
            var package = RegulationBinary.Read(File.ReadAllBytes(Path.Combine(output, "mod", "regulation.bin")));
            Check(untouched.All(table => stock.Table(table).SequenceEqual(package.Table(table))), "global combat data unchanged");
            var buddy = new RegulationParam(package.Table("BuddyParam"), "BUDDY_PARAM_ST", 160).Row(23600000);
            var npcId = RegulationBinary.I32(buddy, 8);
            Check(npcId == (bias < 0 ? 100000020 : 100000021), "private NPC used for a role bias");
            Check(RegulationBinary.I32(buddy, 12) == (setup.CombatPreset is null ? 100000020 : 100000010), "expected buddy AI");
            Check(RegulationBinary.I32(buddy, 40) == 900000 && RegulationBinary.I32(buddy, 80) == 900010, "reinforcement kept");
            var npc = new RegulationParam(package.Table("NpcParam"), "NPC_PARAM_ST", 736).Row(npcId);
            Check(npc[307] == 47 && RegulationBinary.I32(npc, 516) == bias, "friendly team and role bias");
            Check(RegulationBinary.I32(npc, 528) == (setup.Shade == CompanionShade.Puppet ? 295200 : 900011), "shade kept");
            Check(RegulationBinary.I32(npc, 520) == (setup.Shade == CompanionShade.Puppet ? 295201 : -1), "puppet particles");
            var initId = RegulationBinary.I32(buddy, 84);
            var init = new RegulationParam(package.Table("CharaInitParam"), "CHARACTER_INIT_PARAM", 320).Row(initId);
            VerifyHand(package, init, setup.WeaponId, setup.RightAffinity, setup.RightReinforcement, setup.RightAshOfWarId,
                weaponOffset: 16, typeOffset: 232, customId: 900000);
            VerifyHand(package, init, setup.LeftWeaponId ?? setup.WeaponId, setup.LeftAffinity, setup.LeftReinforcement, setup.LeftAshOfWarId,
                weaponOffset: 24, typeOffset: 235, customId: 900001);
            if (setup.RightAmmoId == 50_040_000)
                Check(RegulationBinary.I32(init, 48) == 50_040_000 && RegulationBinary.I32(init, 52) == 52_000_000 &&
                    BinaryPrimitives.ReadUInt16LittleEndian(init[178..]) == 99 &&
                    BinaryPrimitives.ReadUInt16LittleEndian(init[180..]) == 99, "arrow and bolt slots and quantities");
            for (var slot = 0; slot < SpellCatalog.SlotCount; slot++)
                Check(RegulationBinary.I32(init, 96 + slot * 4) == (slot < spells.Length ? spells[slot] : -1), "equipped spells");
            var talismans = setup.TalismanIds ?? [1210, 2130];
            for (var slot = 0; slot < ExtrasCatalog.TalismanSlots; slot++)
                Check(RegulationBinary.I32(init, 64 + slot * 4) == (slot < talismans.Length ? talismans[slot] : -1), "equipped talismans");
            var items = setup.Consumables ?? [];
            for (var slot = 0; slot < 10; slot++)
                Check(RegulationBinary.I32(init, 124 + slot * 4) == (slot < items.Length ? items[slot].Id : -1) &&
                    init[204 + slot] == (slot < items.Length ? items[slot].Quantity : 0), "quick item and quantity");
            Check(RegulationBinary.I32(init, 276) == 10040 && init[300] == 3, "legacy talisman pouches retained");
            Console.WriteLine($"{name} export: AI, gear, spells, talismans, items, shade and tuning verified.");
        }
    }
    finally
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }
}

static void VerifyHand(RegulationBinary package, ReadOnlySpan<byte> init, int id, WeaponAffinity affinity, int level,
    int? ash, int weaponOffset, int typeOffset, int customId)
{
    var variant = id == -1 ? id : id + (int)affinity * 100;
    if (ash is null)
    {
        Check(init[typeOffset] == 0 && RegulationBinary.I32(init, weaponOffset) == variant + level, "direct weapon ID and reinforcement");
        return;
    }
    var custom = new RegulationParam(package.Table("EquipParamCustomWeapon"), "EQUIP_PARAM_CUSTOM_WEAPON_ST", 16).Row(customId);
    Check(init[typeOffset] == 1 && RegulationBinary.I32(init, weaponOffset) == customId &&
        RegulationBinary.I32(custom, 0) == variant && RegulationBinary.I32(custom, 4) == ash &&
        custom[8] == level, "custom weapon affinity, Ash and reinforcement");
}
