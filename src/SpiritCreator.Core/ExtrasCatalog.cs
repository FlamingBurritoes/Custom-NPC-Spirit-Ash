namespace SpiritCreator;

public sealed record TalismanChoice(int Id, string Name, int EffectId)
{
    public override string ToString() => Name;
}

public sealed record ConsumableChoice(int Id, string Name, int MaxQuantity, int AiUsage, int ReferenceId, byte ReferenceCategory)
{
    public override string ToString() => Name;
}

public sealed record ConsumableStack(int Id, int Quantity);

public static class ExtrasCatalog
{
    public const int TalismanSlots = 4;
    public const int ConsumableSlots = 3;

    public static IReadOnlyList<TalismanChoice> Talismans { get; } = Array.AsReadOnly(
        new[] { new TalismanChoice(-1, "None", -1) }.Concat(CatalogData.Read("Talismans")
            .Select(r => new TalismanChoice(CatalogData.Int(r[0]), r[1], CatalogData.Int(r[2])))
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)).ToArray());
    private static IReadOnlyList<TalismanChoice> PreviousTalismans => Array.AsReadOnly(new TalismanChoice[]
    {
        new(EquipmentCatalog.Empty, "None", -1),
        new(2130, "Axe Talisman", 321300),
        new(1210, "Bull-Goat's Talisman", 312100),
        new(1000, "Crimson Amber Medallion", 310000),
        new(7000, "Crimson Amber Medallion +3", 20370000),
        new(4003, "Dragoncrest Greatshield Talisman", 340030),
        new(4000, "Dragoncrest Shield Talisman", 340000),
        new(3040, "Faithful's Canvas Talisman", 330400),
        new(4020, "Flamedrake Talisman", 340200),
        new(3001, "Graven-Mass Talisman", 330010),
        new(3000, "Graven-School Talisman", 330000),
        new(1150, "Green Turtle Talisman", 311500),
        new(3070, "Radagon Icon", 330700),
        new(2050, "Ritual Sword Talisman", 320500),
        new(1020, "Viridian Amber Medallion", 310200)
    });

    // Conservative loadout caps, not the player's inventory capacity.
    public static IReadOnlyList<ConsumableChoice> Consumables { get; } = Array.AsReadOnly(
        new[] { new ConsumableChoice(-1, "None", 0, -1, -1, 0) }.Concat(CatalogData.Read("Items")
            .Select(r => new ConsumableChoice(CatalogData.Int(r[0]), r[1], CatalogData.Int(r[2]),
                CatalogData.Int(r[3]), CatalogData.Int(r[4]), (byte)CatalogData.Int(r[5])))
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)).ToArray());
    private static IReadOnlyList<ConsumableChoice> PreviousConsumables => Array.AsReadOnly(new ConsumableChoice[]
    {
        new(EquipmentCatalog.Empty, "None", 0, -1, -1, 0),
        new(50201, "NPC Crimson Flask", 3, 20070000, 19391, 2),
        new(820, "Boiled Crab", 5, 30000000, 500820, 2),
        new(1210, "Exalted Flesh", 5, 30000000, 3950, 2),
        new(300, "Fire Pot", 10, 10000000, 10030000, 1),
        new(1730, "Kukri", 10, 10000001, 10173000, 1),
        new(1700, "Throwing Dagger", 10, 10000001, 10170000, 1)
    });

    public static string? LoadoutProblem(IReadOnlyList<int>? talismans, IReadOnlyList<ConsumableStack>? items)
    {
        if (talismans is not null)
        {
            if (talismans.Count > TalismanSlots)
                return $"Choose at most {TalismanSlots} talismans.";
            if (talismans.Where(id => id != EquipmentCatalog.Empty).Distinct().Count() !=
                talismans.Count(id => id != EquipmentCatalog.Empty))
                return "Choose each talisman only once.";
            foreach (var id in talismans)
                if (!Talismans.Any(choice => choice.Id == id))
                    return $"Unknown talisman ID {id}.";
        }
        if (items is not null)
        {
            if (items.Count > ConsumableSlots)
                return $"Choose at most {ConsumableSlots} consumable types.";
            var seen = new HashSet<int>();
            foreach (var stack in items)
            {
                if (stack is null || Consumables.FirstOrDefault(choice => choice.Id >= 0 && choice.Id == stack.Id) is not { } item)
                    return "Unknown consumable. Choose an item from the companion catalog.";
                if (!seen.Add(item.Id))
                    return "Choose each consumable only once; set its quantity in that slot.";
                if (stack.Quantity < 1 || stack.Quantity > item.MaxQuantity)
                    return $"{item.Name} quantity must be between 1 and {item.MaxQuantity}.";
            }
        }
        return null;
    }
}
