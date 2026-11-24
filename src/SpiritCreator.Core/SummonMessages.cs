using System.Buffers.Binary;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using static SpiritCreator.RegulationBinary;

namespace SpiritCreator;

// The English item archives shipped with Steam 1.17.3 all contain the base name tables.
internal static class SummonMessageSource
{
    private static readonly (string FileName, string Hash)[] Archives =
    [
        ("item.msgbnd.dcx", "3225627397658E9BC5979F342DF06A39FB8B8C98D7F112C78FF31FDB6CEC53B2"),
        ("item_dlc01.msgbnd.dcx", "02AB3FBA9F7A9CB32193DEE3ED4EC84CA7115A7230C914FB694824F31C929D0D"),
        ("item_dlc02.msgbnd.dcx", "2C8A296496A1E1F6BF9B99ADCE85DA5F7063BA16138A103E731932EBFAEF56AB")
    ];

    internal static Dictionary<string, byte[]> Rename(string gameFolder, string name, int ashId)
    {
        var inputs = new Dictionary<string, byte[]>();
        using var archive = new StockMessageArchive(gameFolder);
        foreach (var (file, hash) in Archives)
        {
            var bytes = archive.ReadEnglishMessage(file);
            var length = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(32));
            Require(length > 0 && length <= bytes.Length - 76, $"Invalid compression length in {file}.");
            // Some extractors leave the BDT's zero padding on the end.
            Array.Resize(ref bytes, length + 76);
            Require(Convert.ToHexString(SHA256.HashData(bytes)) == hash, $"Use the stock Steam 1.17.3 English {file}.");
            inputs.Add(file, bytes);
        }
        using var codec = new MessageOodle(Path.Combine(gameFolder, "oo2core_6_win64.dll"));
        var itemRenamed = false;
        foreach (var file in inputs.Keys.ToArray())
        {
            var dcx = inputs[file];
            var original = codec.Decode(dcx);
            var rewritten = SummonMessages.Rename(original, name, ashId, out var renamed);
            itemRenamed |= renamed;
            var packed = codec.Encode(dcx, rewritten);
            Require(codec.Decode(packed).AsSpan().SequenceEqual(rewritten), $"Message archive readback failed for {file}.");
            inputs[file] = packed;
        }
        Require(itemRenamed, "The selected Spirit Ash was not found in the English item names.");
        return inputs;
    }
}

internal static class SummonMessages
{
    internal const int NpcNameId = 9900000;
    internal const int AshNameId = 236000;
    internal static byte[] Rename(byte[] binder, string name, int ashId, out bool itemRenamed)
    {
        Require(binder.AsSpan(0, 4).SequenceEqual("BND4"u8) && U64(binder, 16) == 64 &&
            U64(binder, 32) == 36 && binder.AsSpan(48, 3).SequenceEqual(new byte[] { 1, 0x74, 4 }),
            "Unsupported item message binder.");
        var headersLength = Offset(U64(binder, 40), binder.Length);
        var headers = binder.AsSpan(0, headersLength).ToArray();
        using var stream = new MemoryStream();
        stream.Write(headers);
        var npcRenamed = false;
        itemRenamed = false;
        for (int i = 0, count = I32(binder, 12); i < count; i++)
        {
            var header = 64 + i * 36;
            var id = I32(binder, header + 28);
            var size = Offset(U64(binder, header + 8), binder.Length);
            var offset = Offset(U32(binder, header + 24), binder.Length);
            ReadOnlySpan<byte> data = binder.AsSpan(offset, size);
            if (id == 18 || id == (ashId < 2_000_000 ? 10 : 319))
            {
                Require(U32(binder, header) == 0x40 && U64(binder, header + 16) == (ulong)size,
                    "Unexpected compressed message entry.");
                var entries = ReadFmg(data);
                if (id == 18)
                {
                    Require(entries.TryAdd(NpcNameId, name), "The private NPC name ID is already in use.");
                    npcRenamed = true;
                }
                else
                {
                    for (var level = 0; level <= 10; level++)
                    {
                        Require(!string.IsNullOrWhiteSpace(entries.GetValueOrDefault(ashId + level)),
                            "Missing an English name for the selected Ash or its upgrades.");
                        var suffix = level == 0 ? "" : $" +{level.ToString(CultureInfo.InvariantCulture)}";
                        entries[ashId + level] = name + suffix;
                    }
                    itemRenamed = true;
                }
                data = WriteFmg(data, entries);
                Require(ReadFmg(data).SequenceEqual(entries), "Name table readback failed.");
                Write64(headers, header + 8, (ulong)data.Length);
                Write64(headers, header + 16, (ulong)data.Length);
            }
            while ((stream.Position & 15) != 0)
                stream.WriteByte(0);
            Write32(headers, header + 24, (int)stream.Position);
            stream.Write(data);
        }
        Require(npcRenamed, "Missing NPC name table.");
        stream.Position = 0;
        stream.Write(headers);
        return stream.ToArray();
    }

    internal static SortedDictionary<int, string?> ReadFmg(ReadOnlySpan<byte> bytes)
    {
        Require(bytes[..4].SequenceEqual("\0\0\x02\0"u8) && I32(bytes, 4) == bytes.Length, "Unsupported FMG layout.");
        var entries = new SortedDictionary<int, string?>();
        var offsets = Offset(U64(bytes, 24), bytes.Length);
        for (int group = 0, groups = I32(bytes, 12); group < groups; group++)
        {
            var pos = 40 + 16 * group;
            var index = I32(bytes, pos);
            for (int id = I32(bytes, pos + 4), last = I32(bytes, pos + 8); id <= last; id++, index++)
            {
                var start = Offset(U64(bytes, offsets + 8 * index), bytes.Length);
                var end = start;
                if (start != 0)
                    while (end + 1 < bytes.Length && (bytes[end] != 0 || bytes[end + 1] != 0))
                        end += 2;
                Require(end + 1 < bytes.Length, "Unterminated FMG name.");
                entries.Add(id, start == 0 ? null : Encoding.Unicode.GetString(bytes[start..end]));
            }
        }
        return entries;
    }

    private static byte[] WriteFmg(ReadOnlySpan<byte> header, SortedDictionary<int, string?> entries)
    {
        var groups = new List<(int Index, int First, int Last)>();
        int index = 0;
        foreach (var id in entries.Keys)
        {
            if (groups.Count > 0 && groups[^1].Last + 1 == id)
                groups[^1] = (groups[^1].Index, groups[^1].First, id);
            else
                groups.Add((index, id, id));
            index++;
        }
        var offsets = 40 + 16 * groups.Count;
        var start = offsets + 8 * entries.Count;
        var result = new byte[start + entries.Values.Sum(s => s is null ? 0 : (s.Length + 1) * 2)];
        header[..40].CopyTo(result);
        Write32(result, 4, result.Length);
        Write32(result, 12, groups.Count);
        Write32(result, 16, entries.Count);
        Write64(result, 24, (ulong)offsets);
        for (var i = 0; i < groups.Count; i++)
        {
            var (firstIndex, first, last) = groups[i];
            Write32(result, 40 + 16 * i, firstIndex);
            Write32(result, 44 + 16 * i, first);
            Write32(result, 48 + 16 * i, last);
        }
        foreach (var text in entries.Values)
        {
            if (text is not null)
            {
                Write64(result, offsets, (ulong)start);
                start += Encoding.Unicode.GetBytes(text.AsSpan(), result.AsSpan(start)) + 2;
            }
            offsets += 8;
        }
        return result;
    }
}

internal sealed class MessageOodle : IDisposable
{
    private readonly nint _library;
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate long DecodeNative(byte[] src, long size, [Out] byte[] dest, long length, int fuzzy, int crc,
        int verbosity, nint buffer, long bufferSize, nint callback, nint userdata, nint memory, long memorySize, int phase);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate long EncodeNative(int compressor, byte[] src, long length, [Out] byte[] dest, int level,
        nint options, nint dictionary, nint lrm, nint scratch, long scratchSize);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint OptionsNative(int compressor, int level);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate long EncodeSizeNative(long length);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate long DecodeSizeNative(long length, int allowCorruption);

    internal MessageOodle(string path)
    {
        Require(File.Exists(path), "Find the stock regulation.bin in your Elden Ring Game folder to locate the game's Oodle library.");
        using var file = File.OpenRead(path);
        Require(Convert.ToHexString(SHA256.HashData(file)) == "988E8226D129B03011A96F51B476E86D38C0C3E817266375A78A7CF3B85D93C4",
            "Unsupported Oodle library; use the stock Steam 1.17.3 Game folder.");
        _library = NativeLibrary.Load(Path.GetFullPath(path));
    }

    private T Get<T>(string export) where T : Delegate =>
        Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(_library, "OodleLZ_" + export));

    internal byte[] Decode(byte[] dcx, int maxBytes = 16_000_000)
    {
        var size = BinaryPrimitives.ReadInt32BigEndian(dcx.AsSpan(28));
        Require(size > 0 && size <= maxBytes, "Invalid decompressed asset size.");
        var capacity = Get<DecodeSizeNative>("GetDecodeBufferSize")(size, 1);
        var raw = new byte[capacity];
        var packedSize = BinaryPrimitives.ReadInt32BigEndian(dcx.AsSpan(32));
        Require(packedSize > 0 && packedSize <= dcx.Length - 76, "Invalid compressed asset size.");
        Require(Get<DecodeNative>("Decompress")(dcx.AsSpan(76, packedSize).ToArray(), packedSize, raw, size, 1, 0, 0, 0, 0, 0, 0, 0, 0, 3)
            == size, "Failed to decompress the game asset.");
        Array.Resize(ref raw, size);
        return raw;
    }

    internal byte[] Encode(byte[] dcx, byte[] bytes)
    {
        const int kraken = 8, level = 6;
        var defaults = Get<OptionsNative>("CompressOptions_GetDefault")(kraken, level);
        var options = Marshal.AllocHGlobal(48);
        try
        {
            var settings = new int[12];
            Marshal.Copy(defaults, settings, 0, settings.Length);
            settings[2] = 1; // Reset at seek boundaries, as required by the game.
            settings[3] = 0x40000;
            Marshal.Copy(settings, 0, options, settings.Length);
            var packed = new byte[Get<EncodeSizeNative>("GetCompressedBufferSizeNeeded")(bytes.Length)];
            var size = Get<EncodeNative>("Compress")(kraken, bytes, bytes.Length, packed, level, options, 0, 0, 0, 0);
            Require(size > 0 && size <= packed.Length, "Failed to compress the game asset.");
            var result = new byte[76 + size];
            dcx.AsSpan(0, 76).CopyTo(result);
            BinaryPrimitives.WriteInt32BigEndian(result.AsSpan(28), bytes.Length);
            BinaryPrimitives.WriteInt32BigEndian(result.AsSpan(32), (int)size);
            packed.AsSpan(0, (int)size).CopyTo(result.AsSpan(76));
            return result;
        }
        finally
        {
            Marshal.FreeHGlobal(options);
        }
    }

    public void Dispose() => NativeLibrary.Free(_library);
}
