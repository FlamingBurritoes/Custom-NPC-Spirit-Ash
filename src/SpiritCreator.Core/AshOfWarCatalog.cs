namespace SpiritCreator;

public sealed record AshOfWarChoice(int GemId, string Name)
{
    public override string ToString() => Name;
}

public static class AshOfWarCatalog
{
    public static AshOfWarChoice Default { get; } = new(-1, "Default weapon skill");

    // The masks are canMountWep and configurableWepAttr00–12 in the verified patch.
    internal sealed record Entry(AshOfWarChoice Choice, int SkillId, byte[] MountMask, int AffinityMask);

    private static Entry Ash(int gemId, string name, int skillId, string mask, int affinities = 0x1FFF) =>
        new(new(gemId, name), skillId, Convert.FromHexString(mask), affinities);

    private static readonly Entry[] Entries = CatalogData.Read("Ashes")
        .Select(r => Ash(CatalogData.Int(r[0]), r[1], CatalogData.Int(r[2]), r[3], CatalogData.Int(r[4])))
        .OrderBy(a => a.Choice.Name, StringComparer.OrdinalIgnoreCase).ToArray();

    public static IReadOnlyList<AshOfWarChoice> ForWeapon(int weaponId) => Mount(weaponId) is { } type
        ? [Default, .. Entries.Where(entry => Allows(entry.MountMask, type.Bit)).Select(entry => entry.Choice)]
        : [Default];

    internal static bool Allows(ReadOnlySpan<byte> mask, int bit) =>
        (mask[bit / 8] & (1 << (bit % 8))) != 0;

    internal static Entry Resolve(int weaponId, int gemId)
    {
        var entry = Entries.FirstOrDefault(ash => ash.Choice.GemId == gemId);
        if (entry is null)
            throw new InvalidDataException($"Unknown Ash of War ID {gemId}.");
        if (Mount(weaponId) is not { } type || !Allows(entry.MountMask, type.Bit))
            throw new InvalidDataException($"{entry.Choice.Name} cannot be applied to {EquipmentCatalog.Weapon(weaponId).Name}.");
        return entry;
    }

    internal static (ushort WeaponType, int Bit)? Mount(int id)
    {
        return EquipmentCatalog.FindWeapon(id) is { MountBit: >= 0 } weapon ? ((ushort)weapon.Type, weapon.MountBit) : null;
    }
}
