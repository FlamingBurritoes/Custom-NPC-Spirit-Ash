using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace SpiritCreator;

public sealed record CharacterSlot(int Number, string Name)
{
    public override string ToString() => $"{Name}  ·  Slot {Number}";
}

public static class SaveAppearance
{
    private const int PcSaveSize = 0x1BA03D0;
    private const int SlotSize = 0x280010;
    private const int GlobalStart = 0x19003A0;
    private const int GlobalSize = 0x60010;
    private const int ProfileSummary = GlobalStart + 0x1964;
    private const int PlayerDataToProjectiles = 0x1B0 + 0xD0 + 0x58 + 0x1C + 0x58 + 0x58
                                               + 0x9010 + 0x74 + 0x8C + 0x18;
    private static readonly UnicodeEncoding Utf16 = new(false, false, true);

    public static SpiritProfile Import(string path, string character) => Import(ReadSave(path), character);

    public static SpiritProfile Import(string path, CharacterSlot slot) => Import(ReadSave(path), slot);

    public static IReadOnlyList<CharacterSlot> ListCharacters(string path) => ListCharacters(ReadSave(path));

    private static byte[] ReadSave(string path)
    {
        using var stream = File.OpenRead(path);
        Require(stream.Length == PcSaveSize, "Expected a PC Elden Ring save backup (.sl2 or .sl2.bak).");
        var bytes = new byte[PcSaveSize];
        stream.ReadExactly(bytes);
        return bytes;
    }

    internal static IReadOnlyList<CharacterSlot> ListCharacters(ReadOnlySpan<byte> bytes)
    {
        Require(bytes.Length == PcSaveSize && bytes[..4].SequenceEqual("BND4"u8) &&
                ReadUInt(bytes, 0x0C) == 12 && ReadUInt(bytes, 0x10) == 0x40 &&
                ReadUInt(bytes, 0x20) == 0x20 && ReadUInt(bytes, 0x28) == 0x300,
            "Unsupported PC save layout.");
        CheckEntry(bytes, 10, GlobalStart, GlobalSize);
        CheckChecksum(bytes.Slice(GlobalStart, GlobalSize), "Global save data");
        var characters = new List<CharacterSlot>();
        for (var slot = 0; slot < 10; slot++)
        {
            if (bytes[ProfileSummary + slot] != 1)
                continue;
            var summary = ProfileSummary + 10 + slot * 0x24C;
            var name = ReadName(bytes.Slice(summary, 32));
            Require(!string.IsNullOrWhiteSpace(name) && !name.Any(char.IsControl), "Invalid character name in save summary.");
            characters.Add(new CharacterSlot(slot + 1, name));
        }
        return characters.AsReadOnly();
    }

    internal static SpiritProfile Import(ReadOnlySpan<byte> bytes, string character)
    {
        var matches = ListCharacters(bytes)
            .Where(slot => string.Equals(slot.Name, character, StringComparison.OrdinalIgnoreCase)).ToArray();
        Require(matches.Length > 0, $"No active character named '{character}' was found in the save.");
        Require(matches.Length == 1, "More than one slot has that name. Use the desktop app to choose a slot number.");
        return ImportSlot(bytes, matches[0]);
    }

    internal static SpiritProfile Import(ReadOnlySpan<byte> bytes, CharacterSlot slot)
    {
        Require(ListCharacters(bytes).Contains(slot),
            "The selected character slot has changed. Open the save again to refresh the list.");
        return ImportSlot(bytes, slot);
    }

    private static SpiritProfile ImportSlot(ReadOnlySpan<byte> bytes, CharacterSlot slot)
    {
        var found = slot.Number - 1;
        var entryStart = 0x300 + found * SlotSize;
        CheckEntry(bytes, found, (uint)entryStart, SlotSize);
        var entry = bytes.Slice(entryStart, SlotSize);
        CheckChecksum(entry, "Selected character slot");
        var data = entry[16..];
        var version = ReadUInt(data, 0);
        Require(version is 210 or 211, $"Unsupported character slot version {version}; this alpha was tested on 210 and 211.");

        var cursor = 0x20;
        for (var i = 0; i < 5120; i++)
        {
            cursor += (ReadUInt(data, cursor) >> 28) switch
            {
                0 or 12 => 8,
                8 => 21,
                9 => 16,
                _ => throw new InvalidDataException("Unsupported item record in the selected character slot.")
            };
        }

        var name = ReadName(data.Slice(cursor + 0x94, 32));
        var body = data[cursor + 0xB6];
        var summaryStart = ProfileSummary + 10 + found * 0x24C;
        Require(name == ReadName(bytes.Slice(summaryStart, 32)) &&
                body is 0 or 1 && body == bytes[summaryStart + 0x242],
            "The selected slot disagrees with its save summary.");
        cursor += PlayerDataToProjectiles;
        var projectiles = ReadUInt(data, cursor);
        const int fixedTail = 4 + 0x9C + 0xC + 0x12F;
        Require(projectiles <= (data.Length - cursor - fixedTail) / 8L, "Invalid projectile table size.");
        cursor += 4 + (int)projectiles * 8 + 0x9C + 0xC;
        var face = data.Slice(cursor, 0x12F);
        Require(ReadUInt(face, 0) == uint.MaxValue && face.Slice(4, 4).SequenceEqual("FACE"u8) &&
                ReadUInt(face, 8) == 4 && ReadUInt(face, 12) == 0x120 && face[0x124] == 1,
            "Unsupported appearance record.");
        Require(ReadUInt(face, 0x127) == uint.MaxValue && ReadUInt(face, 0x12B) == uint.MaxValue,
            "This alpha cannot import DLC/quest face overrides such as altered eyes.");
        Require(face[..0x124].SequenceEqual(bytes.Slice(summaryStart + 0x36, 0x124)),
            "The selected character's face data differs from the save summary.");
        Require(face[0x49] == 0 && face[0x4E] == 0 && face[0x51] == 0, "Unexpected facial geometry layout.");

        var mapped = new Dictionary<string, byte>(FaceMapping.Offsets.Count, StringComparer.Ordinal);
        foreach (var (field, offset) in FaceMapping.Offsets)
            mapped.Add(field, face[offset]);
        var profile = new SpiritProfile
        {
            SchemaVersion = 1, RegulationVersion = SpiritProfile.Regulation, Character = name,
            BodyType = body, WeaponId = EquipmentCatalog.Club, LeftWeaponId = EquipmentCatalog.Empty,
            Attributes = new(), Armor = new(), Face = mapped
        };
        profile.Validate();
        return profile;
    }

    private static void CheckEntry(ReadOnlySpan<byte> bytes, int index, uint start, uint size)
    {
        var header = 0x40 + index * 0x20;
        Require(ReadUInt(bytes, header + 0x08) == size && ReadUInt(bytes, header + 0x0C) == 0 &&
                ReadUInt(bytes, header + 0x10) == start, "Unexpected save entry layout.");
    }

    private static void CheckChecksum(ReadOnlySpan<byte> entry, string label) =>
        Require(MD5.HashData(entry[16..]).AsSpan().SequenceEqual(entry[..16]), $"{label} checksum did not match.");

    private static string ReadName(ReadOnlySpan<byte> data) => Utf16.GetString(data).Split('\0')[0];
    private static uint ReadUInt(ReadOnlySpan<byte> data, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(data[offset..]);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }
}
