using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using BCnEncoder.Decoder;
using BCnEncoder.Shared;
using static SpiritCreator.RegulationBinary;
using static SpiritCreator.StockIconAtlas;

namespace SpiritCreator;

public enum InventoryKind { Weapon, Armor, Talisman, Consumable, Spell, AshOfWar, SpiritAsh }
public readonly record struct InventoryItem(InventoryKind Kind, int Id);

// Keeps only the compressed tiles used by the editor after the stock atlas is read.
// No game images are bundled, exported, or written to a cache on disk.
public sealed class InventoryIcons
{
    public const int IconSize = Size;
    private sealed record Tile(byte[] Blocks, int Width, int Height, int X, int Y);
    private readonly Dictionary<InventoryItem, int> _ids;
    private readonly Dictionary<int, Tile> _tiles;
    private readonly BcDecoder _decoder = new();

    private InventoryIcons(Dictionary<InventoryItem, int> ids, Dictionary<int, Tile> tiles)
    {
        _ids = ids;
        _tiles = tiles;
        _decoder.Options.IsParallel = false;
    }

    public static InventoryIcons Load(string regulation, CancellationToken token = default)
    {
        var path = Path.GetFullPath(regulation);
        using var input = File.OpenRead(path);
        Require(input.Length == RegulationExporter.StockSize, "Item images need the stock Steam 1.17.3 regulation.bin.");
        var bytes = new byte[RegulationExporter.StockSize];
        input.ReadExactly(bytes);
        Require(Convert.ToHexString(SHA256.HashData(bytes)) == RegulationExporter.StockHash,
            "Item images need the stock Steam 1.17.3 regulation.bin.");
        var stock = Read(bytes);
        var folder = Path.GetDirectoryName(path)!;
        using var archive = new StockMessageArchive(folder);
        using var codec = new MessageOodle(Path.Combine(folder, "oo2core_6_win64.dll"));
        var byIcon = ReadTiles(ReadLayout(archive, codec));
        var ids = MapItems(stock, byIcon);
        token.ThrowIfCancellationRequested();
        var atlas = codec.Decode(ReadPackedAtlas(archive), AtlasSize);
        var tiles = new Dictionary<int, Tile>();
        foreach (var icon in ids.Values.Distinct())
        {
            token.ThrowIfCancellationRequested();
            var tile = byIcon[icon];
            var (start, stride) = LocateTile(atlas, tile);
            var x = tile.X % 4;
            var y = tile.Y % 4;
            var width = Size + (x == 0 ? 0 : 4);
            var height = Size + (y == 0 ? 0 : 4);
            var rowBytes = width / 4 * 16;
            var blocks = new byte[rowBytes * (height / 4)];
            for (var row = 0; row < height / 4; row++)
                atlas.AsSpan(start + row * stride, rowBytes).CopyTo(blocks.AsSpan(row * rowBytes));
            tiles.Add(icon, new(blocks, width, height, x, y));
        }
        return new(ids, tiles);
    }

    public byte[]? ReadRgba(InventoryItem item)
    {
        if (!_ids.TryGetValue(item, out var id))
            return null;
        var tile = _tiles[id];
        var pixels = _decoder.DecodeRaw(tile.Blocks, tile.Width, tile.Height, CompressionFormat.Bc7);
        var rgba = new byte[RgbaBytes];
        for (var row = 0; row < Size; row++)
            MemoryMarshal.AsBytes(pixels.AsSpan((row + tile.Y) * tile.Width + tile.X, Size))
                .CopyTo(rgba.AsSpan(row * Size * 4));
        return rgba;
    }

    internal static Dictionary<InventoryItem, int> MapItems(RegulationBinary stock, IReadOnlyDictionary<int, IconTile> tiles)
    {
        var result = new Dictionary<InventoryItem, int>();
        void Add(InventoryKind kind, int id, int icon)
        {
            if (id >= 0 && tiles.ContainsKey(icon))
                result.TryAdd(new(kind, id), icon);
        }
        void Map(InventoryKind kind, string catalog, RegulationParam param, int offset, int fallback = -1)
        {
            foreach (var entry in CatalogData.Read(catalog))
            {
                var id = CatalogData.Int(entry[0]);
                var row = param.Row(id);
                var icon = BinaryPrimitives.ReadUInt16LittleEndian(row[offset..]);
                if (!tiles.ContainsKey(icon) && fallback >= 0)
                    icon = BinaryPrimitives.ReadUInt16LittleEndian(row[fallback..]);
                Add(kind, id, icon);
            }
        }

        var weapons = new RegulationParam(stock.Table("EquipParamWeapon"), "EQUIP_PARAM_WEAPON_ST", 664);
        var goods = new RegulationParam(stock.Table("EquipParamGoods"), "EQUIP_PARAM_GOODS_ST", 176);
        Map(InventoryKind.Weapon, "Weapons", weapons, 190);
        Map(InventoryKind.Weapon, "Ammo", weapons, 190);
        Map(InventoryKind.Armor, "Armor", new(stock.Table("EquipParamProtector"), "EQUIP_PARAM_PROTECTOR_ST", 416), 166, 168);
        Map(InventoryKind.Talisman, "Talismans", new(stock.Table("EquipParamAccessory"), "EQUIP_PARAM_ACCESSORY_ST", 96), 38);
        Map(InventoryKind.Consumable, "Items", goods, 48);
        // The spell's inventory art is on the matching goods row, not the Magic row's effect icon.
        Map(InventoryKind.Spell, "Spells", goods, 48);
        Map(InventoryKind.AshOfWar, "Ashes", new(stock.Table("EquipParamGem"), "EQUIP_PARAM_GEM_ST", 96), 4);
        foreach (var ash in AshTargetCatalog.Options)
            Add(InventoryKind.SpiritAsh, ash.GoodsId, ash.IconId);
        return result;
    }
}
