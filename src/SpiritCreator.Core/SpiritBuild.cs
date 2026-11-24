using System.Text.Json;
using System.Text.Json.Serialization;

namespace SpiritCreator;

// Editable desktop document, distinct from the exported ME3 profile. Quick items
// retain their slot positions here, but are compacted for the regulation exporter.
public sealed record SpiritBuild
{
    private const int MaxBytes = 512 * 1024;
    [JsonRequired]
    public int BuildVersion { get; init; } = 1;
    public required SpiritProfile Profile { get; init; }
    public required ConsumableStack?[] QuickItems { get; init; }
    // Accept older desktop builds without restoring or re-saving their portrait.
    [JsonPropertyName("portraitRgba")]
    public JsonElement? LegacyPortrait { get => null; init { } }
    public Dictionary<int, int[]>? OtherSpellLoadouts { get; init; }

    public SpiritProfile ToExportProfile() => Profile with { Consumables = QuickItems.OfType<ConsumableStack>().ToArray() };

    public void Validate()
    {
        if (BuildVersion != 1 || Profile is null)
            throw new InvalidDataException("Expected a Spirit Creator build, version 1.");
        if (QuickItems is null || QuickItems.Length != ExtrasCatalog.ConsumableSlots || Profile.Consumables is not null)
            throw new InvalidDataException($"The build must have exactly {ExtrasCatalog.ConsumableSlots} quick-item slots.");
        if (Profile.Attributes is null || Profile.Armor is null || Profile.CombatPreset is null || Profile.LeftWeaponId is null)
            throw new InvalidDataException("The build is missing loadout settings.");
        ToExportProfile().Validate(draft: true);
        if (OtherSpellLoadouts is null)
            return;
        foreach (var (weapon, spells) in OtherSpellLoadouts)
        {
            if (weapon == Profile.WeaponId || spells is null || SpellCatalog.ForWeapon(weapon).Count == 0)
                throw new InvalidDataException("Invalid alternate spell loadout.");
            if (SpellCatalog.LoadoutProblem(null, weapon, spells, null) is { } problem)
                throw new InvalidDataException(problem);
        }
    }

    public static SpiritBuild Load(string path)
    {
        using var file = File.OpenRead(path);
        if (file.Length is <= 0 or > MaxBytes)
            throw new InvalidDataException("Choose a Spirit Creator build JSON under 512 KB.");
        var build = JsonSerializer.Deserialize<SpiritBuild>(file, SpiritProfile.JsonOptions)
            ?? throw new InvalidDataException("The build is empty.");
        build.Validate();
        return build;
    }

    public void Save(string path)
    {
        Validate();
        var bytes = JsonSerializer.SerializeToUtf8Bytes(this, SpiritProfile.JsonOptions);
        if (bytes.Length > MaxBytes)
            throw new InvalidDataException("The build is too large to save.");
        path = Path.GetFullPath(path);
        var temp = Path.Combine(Path.GetDirectoryName(path)!, $".spirit-build-{Guid.NewGuid():N}.tmp");
        try
        {
            using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                file.Write(bytes);
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp))
                File.Delete(temp);
        }
    }
}
