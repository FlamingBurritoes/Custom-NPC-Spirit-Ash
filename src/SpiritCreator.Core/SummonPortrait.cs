using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using static SpiritCreator.RegulationBinary;

namespace SpiritCreator;

public static class SummonPortrait
{
    public const int Size = 160;
    public const int RgbaBytes = Size * Size * 4;
    internal const string RelativePath = "menu/hi/01_common.tpf.dcx";
    private const int DdsHeader = 148;
    internal const int AtlasSize = 203701084;
    private const string StockHash = "055798AD5D526A3394A13D63C8F9E5F9FFDE01716CD7B0EB25DFE67528AA4EDE";
    private static readonly int[] Weights = [0, 4, 9, 13, 17, 21, 26, 30, 34, 38, 43, 47, 51, 55, 60, 64];

    internal static byte[] Export(string gameFolder, ReadOnlySpan<byte> rgba, IReadOnlyList<int> iconIds)
    {
        Require(rgba.Length == RgbaBytes, "The portrait must be a 160 × 160 RGBA image.");
        using var archive = new StockMessageArchive(gameFolder);
        using var codec = new MessageOodle(Path.Combine(gameFolder, "oo2core_6_win64.dll"));
        var dcx = ReadPackedAtlas(archive);
        var tiles = ReadTiles(ReadLayout(archive, codec));
        var atlas = codec.Decode(dcx, AtlasSize);
        foreach (var iconId in iconIds)
            Patch(atlas, rgba, FindTile(tiles, iconId));
        var packed = codec.Encode(dcx, atlas);
        Require(codec.Decode(packed, AtlasSize).AsSpan().SequenceEqual(atlas), "Portrait atlas readback failed.");
        return packed;
    }

    internal static byte[] ReadPackedAtlas(StockMessageArchive archive)
    {
        var dcx = archive.ReadAsset("/" + RelativePath, 70_000_000);
        var size = BinaryPrimitives.ReadInt32BigEndian(dcx.AsSpan(32));
        Require(size > 0 && size <= dcx.Length - 76 &&
            Convert.ToHexString(SHA256.HashData(dcx.AsSpan(0, 76 + size))) == StockHash,
            "Unexpected stock 1.17.3 portrait atlas.");
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

    internal static IconTile FindTile(IReadOnlyDictionary<int, IconTile> tiles, int iconId) =>
        tiles.GetValueOrDefault(iconId) ?? throw new InvalidDataException("The selected Ash does not have a compatible icon.");

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
        throw new InvalidDataException("Missing Ash icon texture.");
    }

    internal static void Patch(Span<byte> atlas, ReadOnlySpan<byte> rgba, IconTile tile)
    {
        Require((tile.X % 4 | tile.Y % 4) == 0, "Unexpected Ash icon alignment.");
        var (start, stride) = LocateTile(atlas, tile);
        Span<byte> block = stackalloc byte[64];
        for (var y = 0; y < Size / 4; y++)
            for (var x = 0; x < Size / 4; x++)
            {
                for (var row = 0; row < 4; row++)
                    rgba.Slice(((y * 4 + row) * Size + x * 4) * 4, 16).CopyTo(block[(row * 16)..]);
                EncodeBlock(block, atlas.Slice(start + stride * y + x * 16, 16));
            }
    }

    // BC7 mode 6: two RGBA endpoints and one four-bit index per pixel.
    internal static void EncodeBlock(ReadOnlySpan<byte> pixels, Span<byte> output)
    {
        int first = 0, last = 0, longest = -1;
        for (var a = 0; a < 16; a++)
            for (var b = a; b < 16; b++)
            {
                int distance = 0;
                for (var c = 0; c < 4; c++)
                {
                    var delta = pixels[4 * a + c] - pixels[4 * b + c];
                    distance += delta * delta;
                }
                if (distance > longest)
                    (first, last, longest) = (a, b, distance);
            }
        Span<int> low = stackalloc int[4], high = stackalloc int[4], indices = stackalloc int[16];
        int p0 = Quantize(pixels.Slice(first * 4, 4), low), p1 = Quantize(pixels.Slice(last * 4, 4), high);
        for (var i = 0; i < 16; i++)
        {
            int best = int.MaxValue;
            for (var j = 0; j < 16; j++)
            {
                var w = Weights[j];
                int error = 0;
                for (var c = 0; c < 4; c++)
                {
                    var delta = pixels[4 * i + c] - ((low[c] * (64 - w) + high[c] * w + 32) >> 6);
                    error += delta * delta;
                }
                if (error < best)
                    (indices[i], best) = (j, error);
            }
        }
        if (indices[0] >= 8)
        {
            for (var i = 0; i < 16; i++)
                indices[i] = 15 - indices[i];
            for (var c = 0; c < 4; c++)
                (low[c], high[c]) = (high[c], low[c]);
            (p0, p1) = (p1, p0);
        }
        output.Clear();
        int pos = 0;
        Bits(output, ref pos, 64, 7);
        for (var c = 0; c < 4; c++)
        {
            Bits(output, ref pos, low[c] >> 1, 7);
            Bits(output, ref pos, high[c] >> 1, 7);
        }
        Bits(output, ref pos, p0, 1);
        Bits(output, ref pos, p1, 1);
        for (var i = 0; i < 16; i++)
            Bits(output, ref pos, indices[i], i == 0 ? 3 : 4);
    }

    private static int Quantize(ReadOnlySpan<byte> pixel, Span<int> endpoint)
    {
        // Prefer the alpha parity on ties (keeps opaque and transparent pixels exact).
        var parity = pixel[3] & 1;
        var misses = 0;
        foreach (var value in pixel)
            misses += (value & 1) ^ parity;
        if (misses > 2)
            parity ^= 1;
        for (var c = 0; c < 4; c++)
            endpoint[c] = (pixel[c] & 254) | parity;
        return parity;
    }

    private static void Bits(Span<byte> block, ref int pos, int value, int count)
    {
        for (int i = 0; i < count; i++, pos++)
            block[pos / 8] |= (byte)(((value >> i) & 1) << (pos % 8));
    }
}
