using System.Text.Json.Serialization;

namespace SpiritCreator;

[JsonConverter(typeof(JsonStringEnumConverter<CombatPreset>))]
public enum CombatPreset
{
    Default,
    Warrior,
    Caster,
    Sorcerer
}

public sealed record CombatPresetChoice(CombatPreset Preset, string Name, string Description,
    int? StarterRight = null, int StarterLeft = EquipmentCatalog.Empty)
{
    public override string ToString() => Name;

    public static IReadOnlyList<CombatPresetChoice> Options { get; } = Array.AsReadOnly(new CombatPresetChoice[]
    {
        new(CombatPreset.Default, "Default (Mimic-style)", "Uses your loadout without a role-specific attack bias. Does not copy the player."),
        new(CombatPreset.Warrior, "Warrior", "Favors melee attacks and equips no spells. Use a melee weapon in the right hand.",
            EquipmentCatalog.Club, EquipmentCatalog.HeaterShield),
        new(CombatPreset.Caster, "Caster (Faith)", "Favors spellcasting. Equip an incantation catalyst in the right hand and leave the left hand empty.",
            EquipmentCatalog.FingerSeal),
        new(CombatPreset.Sorcerer, "Sorcerer (Intelligence)", "Favors spellcasting. Equip a sorcery catalyst in the right hand and leave the left hand empty.",
            EquipmentCatalog.GlintstoneStaff)
    });

    // The stock shared humanoid goal uses these effects as switches for additive action weights.
    internal static int? BiasEffect(CombatPreset? preset) => preset switch
    {
        CombatPreset.Warrior => 18657,  // Tragoth: 15043
        CombatPreset.Caster => 18663,   // Wilhelm: 15049
        CombatPreset.Sorcerer => 18667, // Sellen: 15053
        _ => null
    };

    public static string? LoadoutProblem(CombatPreset preset, WeaponChoice right, WeaponChoice left)
    {
        if (!Enum.IsDefined(preset))
            return "Unknown combat preset.";
        if (preset == CombatPreset.Warrior && (right.Kind != WeaponKind.Melee || left.Kind is WeaponKind.Catalyst or WeaponKind.Ranged))
            return "Warrior needs a right-hand melee weapon and cannot equip a catalyst.";
        if (preset == CombatPreset.Caster &&
            (!right.Incantation || left.Id != EquipmentCatalog.Empty))
            return "Caster needs a right-hand incantation catalyst and an empty left hand.";
        if (preset == CombatPreset.Sorcerer &&
            (!right.Sorcery || left.Id != EquipmentCatalog.Empty))
            return "Sorcerer needs a right-hand sorcery catalyst and an empty left hand.";
        return left.Kind == WeaponKind.Catalyst ? "Equip catalysts in the right hand." : null;
    }

}
