using System.Text.Json.Serialization;

namespace SpiritCreator;

[JsonConverter(typeof(JsonStringEnumConverter<CompanionShade>))]
public enum CompanionShade
{
    None,
    SpiritAsh,
    Invader,
    Puppet,
    Cooperator
}

public sealed record ShadeChoice(CompanionShade Shade, string Name)
{
    public override string ToString() => Name;

    public static IReadOnlyList<ShadeChoice> Options { get; } = Array.AsReadOnly(new ShadeChoice[]
    {
        new(CompanionShade.None, "No shade"),
        new(CompanionShade.SpiritAsh, "Spirit Ash (white)"),
        new(CompanionShade.Invader, "Invader (red)"),
        new(CompanionShade.Puppet, "Puppet (blue)"),
        new(CompanionShade.Cooperator, "Cooperator (gold)")
    });
}
