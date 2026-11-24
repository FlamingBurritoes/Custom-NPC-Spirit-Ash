using System.Buffers.Binary;
using System.Text;
using static SpiritCreator.RegulationBinary;

namespace SpiritCreator;

// All writable tables in the pinned file have fixed-size sorted rows and empty names.
internal sealed class RegulationParam
{
    private const int FileHeader = 64, RowHeader = 24;
    private readonly byte[] _stock;
    private readonly int _rowSize, _dataOffset, _typeOffset, _nameOffset;
    private readonly SortedDictionary<int, int> _offsets = new();

    internal RegulationParam(ReadOnlySpan<byte> source, string type, int rowSize)
    {
        Require(source.Length >= FileHeader && source[44] == 0 && source[45] == 0x85 && source[46] == 7,
            $"Unsupported {type} header.");
        _stock = source.ToArray();
        _rowSize = rowSize;
        var count = BinaryPrimitives.ReadUInt16LittleEndian(source[10..]);
        _dataOffset = FileHeader + count * RowHeader;
        _typeOffset = _dataOffset + count * rowSize;
        _nameOffset = _typeOffset + type.Length + 1;
        Require(count > 0 && _nameOffset + 4 == source.Length && U64(source, 48) == (ulong)_dataOffset &&
                U32(source, 0) == _typeOffset && U64(source, 16) == (ulong)_typeOffset &&
                source.Slice(_typeOffset, type.Length).SequenceEqual(Encoding.ASCII.GetBytes(type)) &&
                source[_typeOffset + type.Length] == 0 && U32(source, _nameOffset) == 0,
            $"Unexpected {type} row layout.");
        var previousId = int.MinValue;
        for (var i = 0; i < count; i++)
        {
            var header = FileHeader + i * RowHeader;
            var id = I32(source, header);
            var offset = _dataOffset + i * rowSize;
            Require(id > previousId && U32(source, header + 4) == 0 &&
                    U64(source, header + 8) == (ulong)offset && U64(source, header + 16) == (ulong)_nameOffset,
                $"Unexpected {type} row header.");
            _offsets.Add(id, offset);
            previousId = id;
        }
    }

    internal bool Contains(int id) => _offsets.ContainsKey(id);
    internal IEnumerable<int> Ids => _offsets.Keys;
    internal ReadOnlySpan<byte> Row(int id) => _offsets.TryGetValue(id, out var offset)
        ? _stock.AsSpan(offset, _rowSize) : throw new InvalidDataException($"Missing PARAM row {id}.");

    internal byte[] Write(IReadOnlyDictionary<int, byte[]> replacements)
    {
        Require(replacements.Values.All(row => row.Length == _rowSize), "Invalid replacement row size.");
        var ids = _offsets.Keys.Union(replacements.Keys).Order().ToArray();
        Require(ids.Length <= ushort.MaxValue, "Too many PARAM rows.");
        var shift = (ids.Length - _offsets.Count) * (RowHeader + _rowSize);
        var result = new byte[_stock.Length + shift];
        _stock.AsSpan(0, FileHeader).CopyTo(result);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(10), (ushort)ids.Length);
        Write32(result, 0, _typeOffset + shift);
        Write64(result, 16, (ulong)(_typeOffset + shift));
        var start = FileHeader + ids.Length * RowHeader;
        Write64(result, 48, (ulong)start);
        for (var i = 0; i < ids.Length; i++)
        {
            var id = ids[i];
            var h = FileHeader + i * RowHeader;
            var offset = start + i * _rowSize;
            Write32(result, h, id);
            Write64(result, h + 8, (ulong)offset);
            Write64(result, h + 16, (ulong)(_nameOffset + shift));
            ReadOnlySpan<byte> row = replacements.TryGetValue(id, out var replacement) ? replacement : Row(id);
            row.CopyTo(result.AsSpan(offset));
        }
        _stock.AsSpan(_typeOffset).CopyTo(result.AsSpan(_typeOffset + shift));
        return result;
    }

    internal void Verify(RegulationParam actual, IReadOnlyDictionary<int, byte[]> replacements)
    {
        Require(_offsets.Keys.Union(replacements.Keys).Order().SequenceEqual(actual.Ids), "Rebuilt PARAM row IDs changed.");
        foreach (var id in actual.Ids)
        {
            ReadOnlySpan<byte> expected = replacements.TryGetValue(id, out var row) ? row : Row(id);
            Require(actual.Row(id).SequenceEqual(expected), $"Rebuilt PARAM row {id} changed unexpectedly.");
        }
    }
}
