using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SpiritCreator;

const int slotStart = 0x300;
const int slotSize = 0x280010;
const int global = 0x19003A0;
const int summary = global + 0x1964;
const int profileOffset = summary + 10;

var bytes = new byte[0x1BA03D0];
var data = slotStart + 16;
var player = data + 0x20 + 5120 * 8 + 13; // One weapon record instead of an empty record.
var projectiles = player + 0x94CC;
var face = projectiles + 4 + 8 + 0x9C + 0xC;
"BND4"u8.CopyTo(bytes);
Write(0x0C, 12);
Write(0x10, 0x40);
Write(0x20, 0x20);
Write(0x28, slotStart);
Write(0x48, slotSize);
Write(0x50, slotStart);
Write(0x188, 0x60010);
Write(0x190, global);
Write(data, 211);
Write(data + 0x20, 0x80800000);
Write(projectiles, 1);
Write(face, uint.MaxValue);
"FACE"u8.CopyTo(bytes.AsSpan(face + 4));
Write(face + 8, 4);
Write(face + 12, 0x120);
bytes[face + 0x14] = 9;
bytes[face + 0x35] = 201;
bytes[face + 0xB4] = 173;
bytes[face + 0x124] = 1;
Write(face + 0x127, uint.MaxValue);
Write(face + 0x12B, uint.MaxValue);
bytes[summary] = 1;
bytes[player + 0xB6] = bytes[profileOffset + 0x242] = 1;
Encoding.Unicode.GetBytes("Fixture").CopyTo(bytes, player + 0x94);
Encoding.Unicode.GetBytes("Fixture").CopyTo(bytes, profileOffset);
bytes.AsSpan(face, 0x124).CopyTo(bytes.AsSpan(profileOffset + 0x36));
HashEntry(slotStart, slotSize);
HashEntry(global, 0x60010);

var originalHash = SHA256.HashData(bytes);
var slots = SaveAppearance.ListCharacters(bytes);
Check(slots.SequenceEqual([new CharacterSlot(1, "Fixture")]), "active slot list");
var profile = SaveAppearance.Import(bytes, "fixture");
Check(profile is { Character: "Fixture", BodyType: 1, WeaponId: SpiritProfile.JawboneAxe }, "slot identification");
Check(profile.Face.Count == 157 && profile.Face["hair_partsId"] == 9 &&
      profile.Face["faceGeoData01"] == 201 && profile.Face["chrBodyScaleRLeg"] == 173, "mapping");
Check(originalHash.SequenceEqual(SHA256.HashData(bytes)), "input remained unchanged");
var json = JsonSerializer.Serialize(profile, SpiritProfile.JsonOptions);
var roundTrip = JsonSerializer.Deserialize<SpiritProfile>(json, SpiritProfile.JsonOptions)!;
roundTrip.Validate();
Check(profile.Face.OrderBy(p => p.Key).SequenceEqual(roundTrip.Face.OrderBy(p => p.Key)), "JSON round-trip");
(roundTrip with { WeaponId = SpiritProfile.BattleAxe }).Validate();
Check(roundTrip is { HpPercent: 100, DamagePercent: 100 }, "neutral combat defaults");
var legacyJson = JsonSerializer.SerializeToNode(profile, SpiritProfile.JsonOptions)!.AsObject();
legacyJson.Remove("hpPercent");
legacyJson.Remove("damagePercent");
Check(legacyJson.Deserialize<SpiritProfile>(SpiritProfile.JsonOptions) is { HpPercent: 100, DamagePercent: 100 },
    "old profile defaults");
(profile with { HpPercent = 200, DamagePercent = 50 }).Validate();
Reject(() => (profile with { HpPercent = 49 }).Validate(), "minimum HP");
Reject(() => (profile with { DamagePercent = 201 }).Validate(), "maximum damage");
Check(FaceMapping.Offsets.Values.Select(FaceMapping.ParamOffset).Distinct().Count() == 157,
    "unique destination appearance fields");
var dcxHeader = new byte[76];
BinaryPrimitives.WriteInt32BigEndian(dcxHeader.AsSpan(44), 0x20);
dcxHeader[48] = 21;
var dcx = RegulationBinary.CompressDcx(dcxHeader, new byte[160_000]);
var compressedSize = BinaryPrimitives.ReadInt32BigEndian(dcx.AsSpan(32));
Check(dcx.AsSpan(44, 5).SequenceEqual(new byte[] { 0, 0, 0, 0x20, 15 }), "DCX header length and level");
Check(dcx.AsSpan(76, 6).SequenceEqual(new byte[] { 0x28, 0xB5, 0x2F, 0xFD, 0, 0x30 }),
    "Zstandard frame without content size, 64 KiB window");
Check(compressedSize > 6 && dcx.Length == ((76 + compressedSize + 15) & ~15) &&
      dcx.AsSpan(76 + compressedSize).IndexOfAnyExcept((byte)0) == -1, "DCX zero alignment");
Reject(() => (profile with { WeaponId = 42 }).Validate(), "weapon ID");
Reject(() => (profile with { SchemaVersion = 2 }).Validate(), "schema");
Reject(() => SaveAppearance.Import(bytes, "missing"), "missing slot");
Reject(() => SaveAppearance.Import(bytes, new CharacterSlot(1, "Stale")), "stale slot");
Reject(() => SaveAppearance.Import(bytes.AsSpan(0, 100), "Fixture"), "short save");
bytes[summary + 1] = 1;
bytes.AsSpan(profileOffset, 0x24C).CopyTo(bytes.AsSpan(profileOffset + 0x24C));
HashEntry(global, 0x60010);
Check(SaveAppearance.ListCharacters(bytes).SequenceEqual(
    [new CharacterSlot(1, "Fixture"), new CharacterSlot(2, "Fixture")]), "duplicate names appear as distinct slots");
Reject(() => SaveAppearance.Import(bytes, "Fixture"), "ambiguous name");
Check(SaveAppearance.Import(bytes, slots[0]).Character == "Fixture", "explicit slot with duplicate name");
bytes[face + 0x14]++;
Reject(() => SaveAppearance.Import(bytes, "Fixture"), "slot checksum");
HashEntry(slotStart, slotSize);
Reject(() => SaveAppearance.Import(bytes, "Fixture"), "slot/summary appearance mismatch");
bytes[face + 0x14]--;
Write(face + 0x127, 81000);
HashEntry(slotStart, slotSize);
Reject(() => SaveAppearance.Import(bytes, "Fixture"), "unsupported quest override");
Console.WriteLine("Synthetic save import, mapping, JSON and validation checks passed.");

void Write(int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
void HashEntry(int start, int size) =>
    MD5.HashData(bytes.AsSpan(start + 16, size - 16)).CopyTo(bytes, start);

static void Check(bool condition, string test)
{
    if (!condition)
        throw new Exception($"Failed {test}");
}

static void Reject(Action run, string test)
{
    try
    {
        run();
    }
    catch (InvalidDataException)
    {
        return;
    }
    throw new Exception($"Failed to reject {test}");
}
