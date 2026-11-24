namespace SpiritCreator;

public sealed record AmmoChoice(int Id, string Name, int Type)
{
    public override string ToString() => Name;
}

public static class AmmunitionCatalog
{
    public static IReadOnlyList<AmmoChoice> All { get; } = Array.AsReadOnly(CatalogData.Read("Ammo")
        .Select(r => new AmmoChoice(CatalogData.Int(r[0]), r[1], CatalogData.Int(r[2])))
        .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToArray());

    public static int TypeForWeapon(int id) => EquipmentCatalog.Weapon(id).Type switch
    {
        50 or 51 => 81, 53 => 83, 55 => 85, 56 => 86, _ => 0
    };

    public static AmmoChoice? Default(int type) => All.FirstOrDefault(a => a.Id == (type switch
    {
        81 => 50000000, 83 => 51000000, 85 => 52000000, 86 => 53000000, _ => -1
    }));

    internal static AmmoChoice? Resolve(int weapon, int? selected)
    {
        var type = TypeForWeapon(weapon);
        return selected is null ? Default(type) : All.FirstOrDefault(a => a.Id == selected && a.Type == type)
            ?? throw new InvalidDataException("Ammunition does not match the selected ranged weapon.");
    }
}
