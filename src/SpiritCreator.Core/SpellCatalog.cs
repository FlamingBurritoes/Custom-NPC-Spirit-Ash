namespace SpiritCreator;

public sealed record SpellChoice(int Id, string Name, WeaponRequirements Requirements, int AiUsage, int CastType = 0)
{
    public string AiGroup => (AiUsage / 1_000_000) switch
    {
        10 => "Long range",
        11 => "Mid range",
        12 => "Close range",
        20 => "Healing",
        30 => "Buff",
        -1 or 0 => "No recognized AI action",
        _ => "Special AI usage"
    };

    public override string ToString() => AiGroup.Length == 0 ? Name : $"{Name} · {AiGroup}";
}

public static class SpellCatalog
{
    public const int SlotCount = 7;

    // Curated stock 1.17.3 spells with a known category in the shared humanoid AI.
    // These are AI usage IDs, not custom weights or guarantees that the NPC will cast.
    private static readonly IReadOnlyList<SpellChoice> Sorceries = Sorted(
    [
        Spell(4000, "Glintstone Pebble", 10000100, intelligence: 10),
        Spell(4001, "Great Glintstone Shard", 10000100, intelligence: 16),
        Spell(4010, "Swift Glintstone Shard", 10000100, intelligence: 12),
        Spell(4020, "Glintstone Cometshard", 10000200, intelligence: 36),
        Spell(4021, "Comet", 10000200, intelligence: 52),
        Spell(4030, "Shard Spiral", 10000000, intelligence: 27),
        Spell(4040, "Glintstone Stars", 10000200, intelligence: 12),
        Spell(4050, "Star Shower", 10000200, intelligence: 24),
        Spell(4070, "Glintstone Arc", 10000100, intelligence: 13),
        Spell(4080, "Cannon of Haima", 10000000, intelligence: 25),
        Spell(4120, "Gavel of Haima", 12000000, intelligence: 25),
        Spell(4130, "Terra Magica", 30100000, intelligence: 20),
        Spell(4200, "Comet Azur", 10000200, intelligence: 60),
        Spell(4220, "Stars of Ruin", 10000000, intelligence: 43),
        Spell(4300, "Glintblade Phalanx", 11100000, intelligence: 22),
        Spell(4301, "Carian Phalanx", 11100000, intelligence: 34),
        Spell(4360, "Rennala's Full Moon", 10000000, intelligence: 70),
        Spell(4361, "Ranni's Dark Moon", 10000000, intelligence: 68),
        Spell(4380, "Loretta's Greatbow", 10000200, intelligence: 26),
        Spell(4390, "Magic Glintblade", 10000000, intelligence: 14),
        Spell(4400, "Glintstone Icecrag", 10000000, intelligence: 15),
        Spell(4410, "Zamor Ice Storm", 12000000, intelligence: 36),
        Spell(4430, "Carian Greatsword", 12000000, intelligence: 24),
        Spell(4431, "Adula's Moonblade", 12000000, intelligence: 32),
        Spell(4440, "Carian Slicer", 12000130, intelligence: 14),
        Spell(4450, "Carian Piercer", 11000021, intelligence: 27),
        Spell(4610, "Night Shard", 10000000, intelligence: 18),
        Spell(4620, "Night Comet", 10000000, intelligence: 38),
        Spell(4710, "Rock Sling", 11000000, intelligence: 18),
        Spell(4720, "Gravity Well", 11000000, intelligence: 17),
        Spell(4800, "Magma Shot", 10000000, intelligence: 19, faith: 10),
        Spell(4910, "Briars of Punishment", 10000000, faith: 21),
        Spell(5000, "Rancorcall", 10000000, intelligence: 16, faith: 14),
        Spell(5001, "Ancient Death Rancor", 10000000, intelligence: 34, faith: 24),
        Spell(5100, "Oracle Bubbles", 11000000, intelligence: 19, arcane: 15),
        Spell(6500, "Night Maiden's Mist", 11000000, intelligence: 14),
        Spell(2004310, "Glintblade Trio", 10000100, intelligence: 28),
        Spell(2004320, "Rellana's Twin Moons", 11000020, intelligence: 72),
        Spell(2004500, "Glintstone Nail", 10000200, intelligence: 18),
        Spell(2004910, "Impenetrable Thorns", 11000100, faith: 24),
        Spell(2005000, "Rings of Spectral Light", 11000000, intelligence: 24, faith: 18)
    ]);

    private static readonly IReadOnlyList<SpellChoice> Incantations = Sorted(
    [
        Spell(5040, "Death Lightning", 10000000, faith: 47),
        Spell(6000, "Catch Flame", 12000130, faith: 8),
        Spell(6001, "O, Flame!", 12000130, faith: 16),
        Spell(6010, "Flame Sling", 10000000, faith: 10),
        Spell(6020, "Flame, Fall Upon Them", 10000000, faith: 28),
        Spell(6050, "Flame, Grant Me Strength", 30000000, faith: 15),
        Spell(6100, "Giantsflame Take Thee", 10000000, faith: 30),
        Spell(6110, "Flame of the Fell God", 10100000, faith: 41),
        Spell(6210, "Black Flame", 10000000, faith: 20),
        Spell(6230, "Scouring Black Flame", 11000200, faith: 28),
        Spell(6240, "Black Flame Ritual", 12000000, faith: 42),
        Spell(6300, "Bloodflame Talons", 12000130, faith: 13, arcane: 15),
        Spell(6310, "Bloodboon", 11000000, faith: 14, arcane: 17),
        Spell(6400, "Rejection", 12000000, faith: 12),
        Spell(6410, "Wrath of Gold", 12000000, faith: 32),
        Spell(6420, "Urgent Heal", 20000000, faith: 8),
        Spell(6421, "Heal", 20000000, faith: 12),
        Spell(6422, "Great Heal", 20000000, faith: 15),
        Spell(6423, "Lord's Heal", 20000000, faith: 20),
        Spell(6424, "Erdtree Heal", 20000000, faith: 42),
        Spell(6430, "Blessing's Boon", 20000000, faith: 24),
        Spell(6600, "Golden Vow", 30000000, faith: 25),
        Spell(6700, "Discus of Light", 11000000, intelligence: 13, faith: 13),
        Spell(6701, "Triple Rings of Light", 11000000, intelligence: 23, faith: 23),
        Spell(6720, "Elden Stars", 10100000, faith: 50),
        Spell(6800, "Bestial Sling", 11000000, faith: 10),
        Spell(6810, "Stone of Gurranq", 10000000, faith: 13),
        Spell(6820, "Beast Claw", 11000000, faith: 8),
        Spell(6900, "Lightning Spear", 10000000, faith: 17),
        Spell(6910, "Ancient Dragons' Lightning Strike", 12000000, faith: 26),
        Spell(6920, "Lightning Strike", 10000100, faith: 28),
        Spell(6930, "Honed Bolt", 10000000, faith: 24),
        Spell(7000, "Dragonfire", 10000300, faith: 15, arcane: 12),
        Spell(7020, "Dragonice", 10000300, faith: 15, arcane: 12),
        Spell(7030, "Rotten Breath", 10000300, faith: 15, arcane: 12),
        Spell(7040, "Glintstone Breath", 10000300, faith: 15, arcane: 12),
        Spell(7060, "Dragonclaw", 12000000, faith: 17, arcane: 13),
        Spell(7080, "Dragonmaw", 12000000, faith: 24, arcane: 16),
        Spell(7200, "Pest Threads", 10000000, faith: 11),
        Spell(7210, "Swarm of Flies", 11000000, faith: 11, arcane: 16),
        Spell(7310, "The Flame of Frenzy", 10000200, faith: 16),
        Spell(7320, "Frenzied Burst", 11000200, faith: 22),
        Spell(7500, "Aspects of the Crucible: Tail", 12000000, faith: 27),
        Spell(7510, "Aspects of the Crucible: Horns", 12000000, faith: 27),
        Spell(7530, "Black Blade", 11000000, faith: 46),
        Spell(2006690, "Wrath from Afar", 11000200, faith: 34),
        Spell(2006700, "Light of Miquella", 10000000, faith: 72),
        Spell(2006900, "Knight's Lightning Spear", 11000200, faith: 36),
        Spell(2007210, "Pest-Thread Spears", 11000200, faith: 26),
        Spell(2007600, "Watchful Spirit", 10000000, faith: 26),
        Spell(2007700, "Golden Arcs", 11000200, faith: 22),
        Spell(2007800, "Fire Serpent", 10000200, faith: 16),
        Spell(2007820, "Messmer's Orb", 11000200, faith: 60)
    ]);

    public static IReadOnlyList<SpellChoice> All { get; } = Array.AsReadOnly(CatalogData.Read("Spells").Select(r =>
        new SpellChoice(CatalogData.Int(r[0]), r[1], new(Intelligence: CatalogData.Int(r[2]),
            Faith: CatalogData.Int(r[3]), Arcane: CatalogData.Int(r[4])), CatalogData.Int(r[5]), CatalogData.Int(r[6])))
        .OrderBy(spell => spell.Name, StringComparer.OrdinalIgnoreCase).ToArray());

    public static IReadOnlyList<SpellChoice> ForWeapon(int weaponId)
    {
        var weapon = EquipmentCatalog.Weapon(weaponId);
        return All.Where(spell => spell.CastType == 0 ? weapon.Sorcery : weapon.Incantation).ToArray();
    }

    public static int[] StarterIds(int weaponId) => weaponId switch
    {
        EquipmentCatalog.GlintstoneStaff => [4000],
        EquipmentCatalog.FingerSeal => [6010, 6000],
        _ => EquipmentCatalog.Weapon(weaponId) switch
        {
            { Sorcery: true } => [4000],
            { Incantation: true } => [6010, 6000],
            _ => []
        }
    };

    public static string? LoadoutProblem(CombatPreset? preset, int weaponId, IReadOnlyList<int>? spellIds, NpcAttributes? attributes)
    {
        var ids = spellIds ?? StarterIds(weaponId);
        if (ids.Count > SlotCount)
            return $"Choose at most {SlotCount} spells.";
        if (ids.Count == 0 && preset is CombatPreset.Caster or CombatPreset.Sorcerer)
            return "Choose at least one spell for this casting preset.";
        if (ids.Count > 0 && preset == CombatPreset.Warrior)
            return "Warrior cannot equip spells.";
        var choices = ForWeapon(weaponId);
        if (ids.Count > 0 && choices.Count == 0)
            return "Equip a right-hand catalyst to use spells.";
        if (ids.Distinct().Count() != ids.Count)
            return "Choose each spell only once.";
        foreach (var id in ids)
        {
            var spell = choices.FirstOrDefault(choice => choice.Id == id);
            if (spell is null)
                return $"Spell ID {id} is unavailable for the right-hand catalyst.";
            if (attributes is not null && spell.Requirements.Describe(attributes) is { Length: > 0 } missing)
                return $"{spell.Name} requires {missing}.";
        }
        return null;
    }

    private static SpellChoice Spell(int id, string name, int usage, int intelligence = 0, int faith = 0, int arcane = 0) =>
        new(id, name, new(Intelligence: intelligence, Faith: faith, Arcane: arcane), usage);

    private static IReadOnlyList<SpellChoice> Sorted(SpellChoice[] spells) =>
        Array.AsReadOnly(spells.OrderBy(spell => spell.Name, StringComparer.OrdinalIgnoreCase).ToArray());
}
