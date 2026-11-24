using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using ZstdSharp;
using ZstdSharp.Unsafe;

namespace SpiritCreator;

// Narrow adapter for the archive and PARAM layouts of the pinned 1.17.3 file.
// No third-party game-format code or definitions are bundled.
internal sealed class RegulationBinary
{
    private const int DcxHeaderSize = 76;
    private const int EntryCount = 194;
    private const int EntrySize = 36;
    private const int MaxArchiveSize = 64 * 1024 * 1024;
    // Public format constant for local Elden Ring regulation archives; not account data.
    private static readonly byte[] Key = Convert.FromHexString(
        "99BFFC366A6BC8C6F5827D093602D676C42892A01C207FB024D3AF4E493FEF99");
    private readonly byte[] _dcxHeader;
    private readonly byte[] _binder;
    private readonly int _headerLength;
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private sealed record Entry(int Header, int Offset, int Length);

    private RegulationBinary(byte[] dcxHeader, byte[] binder)
    {
        _dcxHeader = dcxHeader;
        _binder = binder;
        Require(binder.Length >= 64 + EntryCount * EntrySize && binder.AsSpan(0, 4).SequenceEqual("BND4"u8) &&
                binder.AsSpan(24, 8).SequenceEqual("11730034"u8) &&
                U32(binder, 12) == EntryCount && U64(binder, 16) == 64 && U64(binder, 32) == EntrySize &&
                binder[48] == 1 && binder[49] == 0x74 && binder[50] == 4,
            "Unsupported regulation binder layout.");
        _headerLength = Offset(U64(binder, 40), binder.Length);
        Require(_headerLength >= 64 + EntryCount * EntrySize, "Invalid binder metadata length.");
        var end = _headerLength;
        for (var index = 0; index < EntryCount; index++)
        {
            var h = 64 + index * EntrySize;
            var size = Offset(U64(binder, h + 8), binder.Length);
            var offset = Offset(U32(binder, h + 24), binder.Length);
            var nameAt = Offset(U32(binder, h + 32), _headerLength);
            Require(U32(binder, h) == 0x40 && U64(binder, h + 16) == (ulong)size &&
                    U32(binder, h + 28) == index && offset >= end && (offset & 15) == 0 &&
                    size <= binder.Length - offset && nameAt >= 64 + EntryCount * EntrySize && (nameAt & 1) == 0,
                "Unexpected binder entry layout.");
            end = offset + size;
            var nameEnd = nameAt;
            while (nameEnd + 1 < _headerLength && (binder[nameEnd] != 0 || binder[nameEnd + 1] != 0))
                nameEnd += 2;
            Require(nameEnd + 1 < _headerLength, "Unterminated binder entry name.");
            var fullName = Encoding.Unicode.GetString(binder, nameAt, nameEnd - nameAt);
            var name = fullName[(fullName.LastIndexOf('\\') + 1)..];
            Require(name.EndsWith(".param", StringComparison.Ordinal) &&
                    _entries.TryAdd(name[..^6], new Entry(h, offset, size)), "Unexpected binder entry name.");
        }
    }

    internal static RegulationBinary Read(byte[] encrypted)
    {
        Require(encrypted.Length > 16 + DcxHeaderSize && encrypted.Length <= MaxArchiveSize &&
                (encrypted.Length & 15) == 0, "Invalid encrypted regulation size.");
        using var aes = Aes.Create();
        aes.Key = Key;
        var dcx = aes.DecryptCbc(encrypted.AsSpan(16), encrypted.AsSpan(0, 16), PaddingMode.None);
        Require(dcx.AsSpan(0, 4).SequenceEqual("DCX\0"u8) && dcx.AsSpan(24, 4).SequenceEqual("DCS\0"u8) &&
                dcx.AsSpan(36, 8).SequenceEqual("DCP\0ZSTD"u8) &&
                BinaryPrimitives.ReadUInt32BigEndian(dcx.AsSpan(44)) == 0x20 &&
                dcx.AsSpan(68, 8).SequenceEqual("DCA\0\0\0\0\x08"u8),
            "Unsupported DCX compression header.");
        var rawSize = BinaryPrimitives.ReadInt32BigEndian(dcx.AsSpan(28));
        var packedSize = BinaryPrimitives.ReadInt32BigEndian(dcx.AsSpan(32));
        Require(rawSize > 0 && rawSize <= MaxArchiveSize && packedSize > 0 &&
                packedSize <= dcx.Length - DcxHeaderSize, "Invalid DCX payload size.");
        var binder = new byte[rawSize];
        using var decoder = new Decompressor();
        Require(decoder.Unwrap(dcx.AsSpan(DcxHeaderSize, packedSize), binder) == rawSize,
            "Unexpected decompressed binder size.");
        return new RegulationBinary(dcx[..DcxHeaderSize], binder);
    }

    internal ReadOnlySpan<byte> Table(string name) =>
        _entries.TryGetValue(name, out var entry) ? _binder.AsSpan(entry.Offset, entry.Length) :
            throw new InvalidDataException($"Missing {name} table.");

    internal byte[] Pack(IReadOnlyDictionary<string, byte[]> replacements)
    {
        foreach (var name in replacements.Keys)
            Require(_entries.ContainsKey(name), $"Unknown replacement table {name}.");
        var length = _headerLength;
        foreach (var (name, entry) in _entries)
            length = Align16(length) + (replacements.TryGetValue(name, out var bytes) ? bytes.Length : entry.Length);
        Require(length <= MaxArchiveSize, "Replacement exceeds the supported binder size.");
        var binder = new byte[length];
        _binder.AsSpan(0, _headerLength).CopyTo(binder);
        var cursor = _headerLength;
        foreach (var (name, entry) in _entries)
        {
            ReadOnlySpan<byte> bytes = replacements.TryGetValue(name, out var replacement) ? replacement : Table(name);
            cursor = Align16(cursor);
            bytes.CopyTo(binder.AsSpan(cursor));
            Write64(binder, entry.Header + 8, (ulong)bytes.Length);
            Write64(binder, entry.Header + 16, (ulong)bytes.Length);
            Write32(binder, entry.Header + 24, cursor);
            cursor += bytes.Length;
        }

        var dcx = CompressDcx(_dcxHeader, binder);
        using var aes = Aes.Create();
        aes.Key = Key;
        // The DCX is already zero-aligned; the verified mod writer then adds an AES padding block.
        var result = new byte[16 + dcx.Length + 16];
        RandomNumberGenerator.Fill(result.AsSpan(0, 16));
        aes.EncryptCbc(dcx, result.AsSpan(0, 16), result.AsSpan(16), PaddingMode.PKCS7);
        return result;
    }

    internal static byte[] CompressDcx(ReadOnlySpan<byte> header, ReadOnlySpan<byte> content)
    {
        const int level = 15;
        using var encoder = new Compressor(level);
        encoder.SetParameter(ZSTD_cParameter.ZSTD_c_windowLog, 16);
        encoder.SetParameter(ZSTD_cParameter.ZSTD_c_contentSizeFlag, 0);
        var packed = encoder.Wrap(content);
        var dcx = new byte[Align16(DcxHeaderSize + packed.Length)];
        header.CopyTo(dcx);
        BinaryPrimitives.WriteInt32BigEndian(dcx.AsSpan(28), content.Length);
        BinaryPrimitives.WriteInt32BigEndian(dcx.AsSpan(32), packed.Length);
        // DCP header length at 44 is 0x20 (big-endian); compression level follows at 48.
        dcx[48] = level;
        packed.CopyTo(dcx.AsSpan(DcxHeaderSize));
        return dcx;
    }

    internal void Verify(RegulationBinary actual, IReadOnlyDictionary<string, byte[]> replacements)
    {
        Require(_entries.Keys.SequenceEqual(actual._entries.Keys), "Rebuilt binder changed the table list or order.");
        foreach (var name in _entries.Keys)
        {
            ReadOnlySpan<byte> expected = replacements.TryGetValue(name, out var bytes) ? bytes : Table(name);
            Require(actual.Table(name).SequenceEqual(expected), $"Rebuilt binder changed {name} unexpectedly.");
        }
    }

    private static int Align16(int value) => (value + 15) & ~15;
    internal static int Offset(ulong offset, int maximum) => offset <= (ulong)maximum ? (int)offset :
        throw new InvalidDataException("Binary offset out of range.");
    internal static uint U32(ReadOnlySpan<byte> data, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(data[offset..]);
    internal static ulong U64(ReadOnlySpan<byte> data, int offset) => BinaryPrimitives.ReadUInt64LittleEndian(data[offset..]);
    internal static int I32(ReadOnlySpan<byte> data, int offset) => BinaryPrimitives.ReadInt32LittleEndian(data[offset..]);
    internal static void Write32(Span<byte> data, int offset, int value) => BinaryPrimitives.WriteInt32LittleEndian(data[offset..], value);
    internal static void Write64(Span<byte> data, int offset, ulong value) => BinaryPrimitives.WriteUInt64LittleEndian(data[offset..], value);
    internal static void Require(bool valid, string message)
    {
        if (!valid)
            throw new InvalidDataException(message);
    }
}
