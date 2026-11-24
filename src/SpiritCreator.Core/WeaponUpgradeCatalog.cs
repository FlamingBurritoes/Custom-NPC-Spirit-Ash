using System.Text.Json.Serialization;

namespace SpiritCreator;

[JsonConverter(typeof(JsonStringEnumConverter<WeaponAffinity>))]
public enum WeaponAffinity
{
    Standard, Heavy, Keen, Quality, Fire, FlameArt, Lightning, Sacred, Magic, Cold, Poison, Blood, Occult
}

public sealed record AffinityChoice(WeaponAffinity Affinity, string Name)
{
    public override string ToString() => Name;
}

public sealed record ReinforcementChoice(int Level)
{
    public override string ToString() => $"+{Level}";
}

public static class WeaponUpgradeCatalog
{
    private static readonly AffinityChoice[] Affinities = Enum.GetValues<WeaponAffinity>()
        .Select(a => new AffinityChoice(a, a == WeaponAffinity.FlameArt ? "Flame Art" : a.ToString())).ToArray();
    private static readonly IReadOnlyList<ReinforcementChoice>[] LevelLists = Enumerable.Range(0, 26).Select(Levels).ToArray();

    private static IReadOnlyList<ReinforcementChoice> Levels(int max) =>
        Array.AsReadOnly(Enumerable.Range(0, max + 1).Select(level => new ReinforcementChoice(level)).ToArray());

    public static IReadOnlyList<ReinforcementChoice> Reinforcements(WeaponChoice weapon) =>
        LevelLists[weapon.MaxLevel];

    public static IReadOnlyList<AffinityChoice> ForWeapon(int weaponId, int? ashId)
    {
        var mask = EquipmentCatalog.Weapon(weaponId).AffinityMask &
            (ashId is { } id ? AshOfWarCatalog.Resolve(weaponId, id).AffinityMask : 0x1FFF);
        return Affinities.Where(choice => (mask & (1 << (int)choice.Affinity)) != 0).ToArray();
    }

    internal static void Validate(WeaponChoice weapon, int? ashId, WeaponAffinity affinity, int level)
    {
        if (!ForWeapon(weapon.Id, ashId).Any(choice => choice.Affinity == affinity))
            throw new InvalidDataException($"{affinity} affinity is not available for {weapon.Name} with this skill.");
        if (level < 0 || level >= Reinforcements(weapon).Count)
            throw new InvalidDataException($"{weapon.Name} supports reinforcement up to +{Reinforcements(weapon).Count - 1}.");
    }

    internal static int VariantId(int weaponId, WeaponAffinity affinity) => weaponId + 100 * (int)affinity;
}
