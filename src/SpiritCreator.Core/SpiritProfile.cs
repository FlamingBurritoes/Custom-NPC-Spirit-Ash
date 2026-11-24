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
    public required Dictionary<string, byte> Face { get; init; }

    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowDuplicateProperties = false
    };

    public void Validate()
    {
        if (SchemaVersion != 1 || RegulationVersion != Regulation)
            throw new InvalidDataException("Expected profile schema 1 and Elden Ring 1.17.3 (11730034).");
        if (string.IsNullOrWhiteSpace(Character) || Character.Length > 16 ||
            Character.Any(char.IsControl) || BodyType > 1)
            throw new InvalidDataException("Invalid character name or body type.");
        if (!SupportedWeapons.Any(weapon => weapon.Id == WeaponId))
            throw new InvalidDataException($"Unknown alpha weapon ID {WeaponId}. Choose {string.Join(" or ", SupportedWeapons.Select(weapon => $"{weapon.Name} ({weapon.Id})"))}.");
        if (Face is null || !FaceMapping.Offsets.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(Face.Keys))
            throw new InvalidDataException("The profile does not contain the expected appearance fields.");
        if (Face["accessories_partsId"] != 0 || Face["decal_partsId"] != 0)
            throw new InvalidDataException("The alpha does not support face accessories or decals.");
    }

    public static IReadOnlyList<WeaponChoice> SupportedWeapons { get; } = Array.AsReadOnly(new[]
    {
        new WeaponChoice(JawboneAxe, "Jawbone Axe"),
        new WeaponChoice(BattleAxe, "Battle Axe")
    });
}

public sealed record WeaponChoice(int Id, string Name)
{
    public override string ToString() => Name;
}
