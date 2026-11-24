namespace SpiritCreator;

public sealed record NpcAttributes(int Vigor = 10, int Mind = 10, int Endurance = 10,
    int Strength = 10, int Dexterity = 10, int Intelligence = 10, int Faith = 10, int Arcane = 10)
{
    public const int Minimum = 10, Maximum = 99;
    [System.Text.Json.Serialization.JsonIgnore]
    public int Level => 1 + Values.Sum(stat => stat - Minimum);
    [System.Text.Json.Serialization.JsonIgnore]
    public int[] Values => [Vigor, Mind, Endurance, Strength, Dexterity, Intelligence, Faith, Arcane];

    internal void Validate()
    {
        if (Values.Any(value => value is < Minimum or > Maximum))
            throw new InvalidDataException($"Attributes must be whole numbers between {Minimum} and {Maximum}.");
    }
}

public sealed record WeaponRequirements(int Strength = 0, int Dexterity = 0, int Intelligence = 0, int Faith = 0, int Arcane = 0)
{
    public string Describe(NpcAttributes? available = null)
    {
        var values = new[] { Strength, Dexterity, Intelligence, Faith, Arcane };
        var names = new[] { "STR", "DEX", "INT", "FAI", "ARC" };
        var stats = available?.Values;
        return string.Join(", ", Enumerable.Range(0, values.Length)
            .Where(i => values[i] > (stats?[i + 3] ?? 0))
            .Select(i => $"{names[i]} {values[i]}"));
    }
}

public enum WeaponKind { Melee, Shield, Catalyst, Empty, Ranged }

public sealed record WeaponChoice(int Id, string Name, WeaponRequirements Requirements, bool Tested = false,
    WeaponKind Kind = WeaponKind.Melee, bool Somber = false, int Type = 0,
    int MaxLevel = 0, int AffinityMask = 1, int MountBit = -1, bool Sorcery = false, bool Incantation = false)
{
    public override string ToString() => Name;
}

public enum ArmorSlot { Head, Chest, Hands, Legs }

public sealed record ArmorLoadout(int Head = -1, int Chest = -1, int Hands = -1, int Legs = -1)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public int[] Ids => [Head, Chest, Hands, Legs];
}

public sealed record ArmorChoice(int Id, string Name, ArmorSlot Slot)
{
    public override string ToString() => Name;
}

public static class EquipmentCatalog
{
    public const int Empty = -1;
    public const int Club = 11010000;
    public const int HeaterShield = 31330000;
    public const int GlintstoneStaff = 33000000;
    public const int FingerSeal = 34000000;
    public static IReadOnlyList<WeaponChoice> Weapons { get; } = Array.AsReadOnly(
        new[] { new WeaponChoice(Empty, "Empty hand", new(), Kind: WeaponKind.Empty) }
        .Concat(CatalogData.Read("Weapons").Select(row =>
        {
            int N(int i) => CatalogData.Int(row[i]);
            var kind = N(7) switch
            {
                57 or 61 => WeaponKind.Catalyst,
                65 or 67 or 69 or 90 => WeaponKind.Shield,
                50 or 51 or 53 or 55 or 56 => WeaponKind.Ranged,
                _ => WeaponKind.Melee
            };
            return new WeaponChoice(N(0), row[1], new(N(2), N(3), N(4), N(5), N(6)),
                N(0) is 14000000 or 14030000, kind, N(8) == 10, N(7), N(8), N(9), N(10), N(11) != 0, N(12) != 0);
        }).OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)).ToArray());
    private static readonly Dictionary<int, WeaponChoice> ById = Weapons.ToDictionary(weapon => weapon.Id);
    private static IReadOnlyList<WeaponChoice> PreviousWeapons => Array.AsReadOnly(
        new[] { new WeaponChoice(Empty, "Empty hand", new(), Kind: WeaponKind.Empty) }.Concat(new WeaponChoice[]
        {
            new(14000000, "Battle Axe", new(12, 8), true),
            new(14030000, "Jawbone Axe", new(14, 8), true),
            new(14020000, "Hand Axe", new(9, 8)),
            new(14140000, "Stormhawk Axe", new(19, 15), Somber: true),
            new(1000000, "Dagger", new(5, 9)),
            new(2000000, "Longsword", new(10, 10)),
            new(2010000, "Short Sword", new(8, 10)),
            new(2020000, "Broadsword", new(10, 10)),
            new(3000000, "Bastard Sword", new(16, 10)),
            new(3030000, "Lordsworn’s Greatsword", new(16, 10)),
            new(3180000, "Claymore", new(16, 13)),
            new(5000000, "Estoc", new(11, 13)),
            new(5020000, "Rapier", new(7, 12)),
            new(7000000, "Falchion", new(9, 13)),
            new(7140000, "Scimitar", new(7, 13)),
            new(9000000, "Uchigatana", new(11, 15)),
            new(9040000, "Rivers of Blood", new(12, 18, Arcane: 20), Somber: true),
            new(10000000, "Twinblade", new(10, 18)),
            new(11000000, "Mace", new(12, 7)),
            new(Club, "Club", new(10)),
            new(11050000, "Morning Star", new(12, 8)),
            new(16000000, "Short Spear", new(10, 10)),
            new(16010000, "Spear", new(12, 15)),
            new(16050000, "Partisan", new(15, 12)),
            new(18000000, "Halberd", new(14, 12)),
            new(18020000, "Lucerne", new(15, 12)),
            new(21000000, "Caestus", new(8, 8)),
            new(21010000, "Spiked Caestus", new(8, 10)),
            new(21070000, "Iron Ball", new(11, 8)),
            new(30000000, "Buckler", new(8, 13), Kind: WeaponKind.Shield),
            new(31130000, "Brass Shield", new(16), Kind: WeaponKind.Shield),
            new(HeaterShield, "Heater Shield", new(10), Kind: WeaponKind.Shield),
            new(GlintstoneStaff, "Glintstone Staff", new(6, Intelligence: 10), Kind: WeaponKind.Catalyst),
            new(FingerSeal, "Finger Seal", new(4, Faith: 10), Kind: WeaponKind.Catalyst)
        }.OrderBy(item => item.Name, StringComparer.Ordinal)).ToArray());

    private static readonly IReadOnlyList<ArmorChoice> ArmorItems = CatalogData.Read("Armor")
        .Select(r => new ArmorChoice(CatalogData.Int(r[0]), r[1], (ArmorSlot)CatalogData.Int(r[2])))
        .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToArray();

    private static ArmorChoice[] BuildArmor()
    {
        List<ArmorChoice> items = [];
        void Set(int id, string head, string chest, string hands, string legs)
        {
            items.Add(new(id, head, ArmorSlot.Head));
            items.Add(new(id + 100, chest, ArmorSlot.Chest));
            items.Add(new(id + 200, hands, ArmorSlot.Hands));
            items.Add(new(id + 300, legs, ArmorSlot.Legs));
        }
        Set(630000, "Astrologer Hood", "Astrologer Robe", "Astrologer Gloves", "Astrologer Trousers");
        Set(660000, "Vagabond Knight Helm", "Vagabond Knight Armor", "Vagabond Knight Gauntlets", "Vagabond Knight Greaves");
        Set(720000, "Beast Champion Helm", "Beast Champion Armor", "Beast Champion Gauntlets", "Beast Champion Greaves");
        Set(730000, "Champion Headband", "Champion Pauldron", "Champion Bracers", "Champion Gaiters");
        Set(870000, "Land of Reeds Helm", "Land of Reeds Armor", "Land of Reeds Gauntlets", "Land of Reeds Greaves");
        Set(880000, "Confessor Hood", "Confessor Armor", "Confessor Gloves", "Confessor Boots");
        Set(1100000, "Chain Coif", "Chain Armor", "Chain Gauntlets", "Chain Leggings");
        Set(1500000, "Knight Helm", "Knight Armor", "Knight Gauntlets", "Knight Greaves");
        items.AddRange([
            new(40000, "Iron Helmet", ArmorSlot.Head), new(40300, "Leather Trousers", ArmorSlot.Legs),
            new(1400100, "Leather Armor", ArmorSlot.Chest), new(1400200, "Leather Gloves", ArmorSlot.Hands),
            new(1400300, "Leather Boots", ArmorSlot.Legs), new(881000, "Confessor Hood (Altered)", ArmorSlot.Head),
            new(881100, "Confessor Armor (Altered)", ArmorSlot.Chest), new(871100, "Land of Reeds Armor (Altered)", ArmorSlot.Chest),
            new(661100, "Vagabond Knight Armor (Altered)", ArmorSlot.Chest), new(631100, "Astrologer Robe (Altered)", ArmorSlot.Chest),
            new(721100, "Beast Champion Armor (Altered)", ArmorSlot.Chest)
        ]);
        return items.OrderBy(i => i.Name, StringComparer.Ordinal).ToArray();
    }

    public static IReadOnlyList<ArmorChoice> ArmorFor(ArmorSlot slot) =>
        [new(Empty, "None", slot), .. ArmorItems.Where(item => item.Slot == slot)];

    public static WeaponChoice Weapon(int id) => ById.GetValueOrDefault(id)
        ?? throw new InvalidDataException($"Unknown weapon ID {id}.");
    internal static WeaponChoice? FindWeapon(int id) => ById.GetValueOrDefault(id);
}
