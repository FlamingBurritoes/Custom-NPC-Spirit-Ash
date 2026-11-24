namespace SpiritCreator;

public sealed record AshSpawnChoice(int RowId, int TriggerEffectId, string Name)
{
    public override string ToString() => $"{Name} · {RowId}";
}

public sealed record AshTargetChoice(int GoodsId, string Name, int IconId, IReadOnlyList<AshSpawnChoice> Rows)
{
    public override string ToString() => Name;
}

public static class AshTargetCatalog
{
    public const int DefaultGoodsId = 236000;

    public static IReadOnlyList<AshTargetChoice> Options { get; } = Array.AsReadOnly(CatalogData.Read("AshTargets")
        .GroupBy(r => CatalogData.Int(r[0]))
        .Select(g => new AshTargetChoice(g.Key, g.First()[1], CatalogData.Int(g.First()[4]),
            Array.AsReadOnly(g.Select(r => new AshSpawnChoice(CatalogData.Int(r[2]), CatalogData.Int(r[3]), r[5]))
                .OrderBy(row => row.RowId).ToArray())))
        .OrderBy(ash => ash.Name, StringComparer.OrdinalIgnoreCase).ToArray());

    internal static AshTargetChoice Find(int? goodsId) =>
        Options.FirstOrDefault(ash => ash.GoodsId == (goodsId ?? DefaultGoodsId))
            ?? throw new InvalidDataException("Choose a supported Spirit Ash.");

    internal static (AshTargetChoice Ash, AshSpawnChoice Row) Resolve(int? goodsId, int? rowId)
    {
        var ash = Find(goodsId);
        var row = rowId is null && ash.Rows.Count == 1 ? ash.Rows[0] :
            ash.Rows.FirstOrDefault(row => row.RowId == rowId);
        return (ash, row ?? throw new InvalidDataException($"Choose a valid spawn row for {ash.Name}."));
    }
}
