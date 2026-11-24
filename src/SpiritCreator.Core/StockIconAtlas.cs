using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using static SpiritCreator.RegulationBinary;

namespace SpiritCreator;

internal static class StockIconAtlas
{
    public const int Size = 160;
    public const int RgbaBytes = Size * Size * 4;
    private const int DdsHeader = 148;
    internal const int AtlasSize = 203701084;
    private const string StockHash = "055798AD5D526A3394A13D63C8F9E5F9FFDE01716CD7B0EB25DFE67528AA4EDE";

    internal static byte[] ReadPackedAtlas(StockMessageArchive archive)
    {
        var dcx = archive.ReadAsset("/menu/hi/01_common.tpf.dcx", 70_000_000);
        var size = BinaryPrimitives.ReadInt32BigEndian(dcx.AsSpan(32));
        Require(size > 0 && size <= dcx.Length - 76 &&
            Convert.ToHexString(SHA256.HashData(dcx.AsSpan(0, 76 + size))) == StockHash,
            "Unexpected stock 1.17.3 inventory icon atlas.");
        return dcx;
    }

    internal static byte[] ReadLayout(StockMessageArchive archive, MessageOodle codec)
    {
        var layout = archive.ReadAsset("/menu/hi/01_common.sblytbnd.dcx", 32_000);
        var layoutSize = BinaryPrimitives.ReadInt32BigEndian(layout.AsSpan(32));
        Require(layoutSize > 0 && layoutSize <= layout.Length - 76 &&
            Convert.ToHexString(SHA256.HashData(layout.AsSpan(0, 76 + layoutSize))) ==
            "322FFAC0CDE5E52BFC0230809D9E1315B9F7E3DD7B78D80F51365A2CCA2D1C2E", "Unexpected stock icon layout.");
        return codec.Decode(layout);
    }

    internal sealed record IconTile(string Texture, int X, int Y);

    internal static IReadOnlyDictionary<int, IconTile> ReadTiles(byte[] layout)
    {
        const string prefix = "MENU_ItemIcon_";
        var result = new Dictionary<int, IconTile>();
        for (var i = 0; i < I32(layout, 12); i++)
        {
            var header = 64 + 36 * i;
            var data = layout.AsSpan(I32(layout, header + 24), I32(layout, header + 8));
            if (!data.StartsWith("<TextureAtlas"u8) || data.IndexOf("MENU_ItemIcon_"u8) < 0)
                continue;
            var xml = XElement.Parse(Encoding.UTF8.GetString(data).TrimEnd('\0'));
            if ((string?)xml.Attribute("imagePath") is not { } texture)
                continue;
            foreach (var tile in xml.Elements("SubTexture"))
                if ((string?)tile.Attribute("name") is { } name && name.StartsWith(prefix, StringComparison.Ordinal) &&
                    name.EndsWith(".png", StringComparison.Ordinal) &&
                    int.TryParse(name.AsSpan(prefix.Length, name.Length - prefix.Length - 4), out var id) &&
                    (int?)tile.Attribute("width") == Size && (int?)tile.Attribute("height") == Size &&
                    (int?)tile.Attribute("x") is { } x && (int?)tile.Attribute("y") is { } y)
                    result.TryAdd(id, new(Path.GetFileNameWithoutExtension(texture), x, y));
        }
        return result;
    }

    internal static (int Start, int Stride) LocateTile(ReadOnlySpan<byte> atlas, IconTile tile)
    {
        Require(atlas.Length == AtlasSize && atlas[..4].SequenceEqual("TPF\0"u8), "Unexpected stock TPF layout.");
        var name = Encoding.Unicode.GetBytes(tile.Texture + "\0");
        for (var i = 0; i < I32(atlas, 8); i++)
        {
            var header = 16 + 20 * i;
            var nameOffset = I32(atlas, header + 12);
            if (!atlas[nameOffset..].StartsWith(name))
                continue;
            var dds = I32(atlas, header);
            var width = I32(atlas, dds + 16);
            var height = I32(atlas, dds + 12);
            Require(atlas.Slice(dds, 4).SequenceEqual("DDS "u8) && I32(atlas, dds + 28) == 1 &&
                atlas.Slice(dds + 84, 4).SequenceEqual("DX10"u8) && I32(atlas, dds + 128) == 98 &&
                tile.X >= 0 && tile.Y >= 0 && width % 4 == 0 &&
                tile.X + Size <= width && tile.Y + Size <= height, "Unexpected stock item icon texture format.");
            var stride = width / 4 * 16;
            return (dds + DdsHeader + tile.Y / 4 * stride + tile.X / 4 * 16, stride);
        }
        throw new InvalidDataException("Missing inventory icon texture.");
    }
}
