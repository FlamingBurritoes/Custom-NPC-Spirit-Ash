using System.Text.Json;
using System.Text.Json.Serialization;

namespace SpiritCreator;

public sealed record SpiritProfile
{
    internal const string Regulation = "11730034";
    internal const int JawboneAxe = 14030000;
    internal const int BattleAxe = 14000000;

    public required int SchemaVersion { get; init; }
    public required string RegulationVersion { get; init; }
    public required string Character { get; init; }
    public required byte BodyType { get; init; }
    public required int WeaponId { get; init; }
    public int? LeftWeaponId { get; init; }
    public int? RightAmmoId { get; init; }
    public int? LeftAmmoId { get; init; }
    public int? RightAshOfWarId { get; init; }
    public int? LeftAshOfWarId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public WeaponAffinity RightAffinity { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public WeaponAffinity LeftAffinity { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int RightReinforcement { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int LeftReinforcement { get; init; }
    public NpcAttributes? Attributes { get; init; }
    public ArmorLoadout? Armor { get; init; }
    public CompanionShade Shade { get; init; }
    public CombatPreset? CombatPreset { get; init; }
    public int[]? SpellIds { get; init; }
    public int[]? TalismanIds { get; init; }
    public ConsumableStack[]? Consumables { get; init; }
    public required Dictionary<string, byte> Face { get; init; }
    public int HpPercent { get; init; } = 100;
    public int DamagePercent { get; init; } = 100;
    public const int MinPercent = 50, MaxPercent = 200;

    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowDuplicateProperties = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public void Validate()
    {
        if (SchemaVersion != 1 || RegulationVersion != Regulation)
            throw new InvalidDataException("Expected profile schema 1 and Elden Ring 1.17.3 (11730034).");
        if (string.IsNullOrWhiteSpace(Character) || Character.Length > 16 ||
            Character.Any(char.IsControl) || BodyType > 1)
            throw new InvalidDataException("Invalid character name or body type.");
        Attributes?.Validate();
        if (!Enum.IsDefined(Shade))
            throw new InvalidDataException("Unknown companion shade.");
        var right = EquipmentCatalog.Weapon(WeaponId);
        var left = EquipmentCatalog.Weapon(LeftWeaponId ?? WeaponId);
        AmmunitionCatalog.Resolve(right.Id, RightAmmoId);
        AmmunitionCatalog.Resolve(left.Id, LeftAmmoId);
        if (CombatPresetChoice.LoadoutProblem(CombatPreset ?? SpiritCreator.CombatPreset.Default, right, left) is { } problem)
            throw new InvalidDataException(problem);
        if (SpellCatalog.LoadoutProblem(CombatPreset, WeaponId, SpellIds, Attributes) is { } spellProblem)
            throw new InvalidDataException(spellProblem);
        if (ExtrasCatalog.LoadoutProblem(TalismanIds, Consumables) is { } extrasProblem)
            throw new InvalidDataException(extrasProblem);
        WeaponUpgradeCatalog.Validate(right, RightAshOfWarId, RightAffinity, RightReinforcement);
        WeaponUpgradeCatalog.Validate(left, LeftAshOfWarId, LeftAffinity, LeftReinforcement);
        if (Armor is { } armor)
            foreach (var slot in Enum.GetValues<ArmorSlot>())
                if (!EquipmentCatalog.ArmorFor(slot).Any(item => item.Id == armor.Ids[(int)slot]))
                    throw new InvalidDataException($"Unknown or incompatible {slot.ToString().ToLowerInvariant()} armor ID.");
        if (HpPercent is < MinPercent or > MaxPercent || DamagePercent is < MinPercent or > MaxPercent)
            throw new InvalidDataException($"HP and damage must each be between {MinPercent}% and {MaxPercent}%.");
        if (Face is null || !FaceMapping.Offsets.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(Face.Keys))
            throw new InvalidDataException("The profile does not contain the expected appearance fields.");
        if (Face["accessories_partsId"] != 0 || Face["decal_partsId"] != 0)
            throw new InvalidDataException("The alpha does not support face accessories or decals.");
    }

}
