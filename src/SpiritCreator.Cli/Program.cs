using System.Globalization;
using System.Text.Json;
using SpiritCreator;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
try
{
    if (args.Length == 0 || args is ["--help"] or ["-h"])
    {
        Console.WriteLine("""
            Spirit Creator CLI — offline Elden Ring 1.17.3 alpha
              import --save FILE --character NAME --profile NEW.json
              export --profile FILE.json --regulation FILE --out NEW_DIRECTORY
            """);
        return 0;
    }

    switch (args[0])
    {
        case "import":
        {
            var options = Options(args, "save", "character", "profile");
            var profile = SaveAppearance.Import(options["save"], options["character"]);
            using (var output = new FileStream(options["profile"], FileMode.CreateNew, FileAccess.Write))
                JsonSerializer.Serialize(output, profile, SpiritProfile.JsonOptions);
            Console.WriteLine($"Imported {profile.Character} to {Path.GetFullPath(options["profile"])}. Save not modified.");
            return 0;
        }
        case "export":
        {
            var options = Options(args, "profile", "regulation", "out");
            using var input = File.OpenRead(options["profile"]);
            var profile = JsonSerializer.Deserialize<SpiritProfile>(input, SpiritProfile.JsonOptions)
                          ?? throw new InvalidDataException("Profile is empty.");
            profile.Validate();
            var path = RegulationExporter.Export(profile, options["regulation"], options["out"]);
            Console.WriteLine($"Exported {profile.Character}: {path}");
            Console.WriteLine($"Launch offline: me3 launch --profile \"{path}\"");
            return 0;
        }
        default:
            throw new ArgumentException($"Unknown command '{args[0]}'. Run with --help.");
    }
}
catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException or
                           UnauthorizedAccessException or JsonException or InvalidOperationException or
                           System.Xml.XmlException or System.ComponentModel.Win32Exception or TimeoutException)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}

static Dictionary<string, string> Options(string[] args, params string[] required)
{
    var options = new Dictionary<string, string>(StringComparer.Ordinal);
    for (var i = 1; i < args.Length; i += 2)
    {
        if (i + 1 >= args.Length || !args[i].StartsWith("--", StringComparison.Ordinal) ||
            args[i + 1].StartsWith("--", StringComparison.Ordinal))
            throw new ArgumentException($"Expected a value for '{args[i]}'.");
        var name = args[i][2..];
        if (!required.Contains(name) || !options.TryAdd(name, args[i + 1]) || string.IsNullOrWhiteSpace(args[i + 1]))
            throw new ArgumentException($"Unknown, empty, or repeated option '{args[i]}'.");
    }
    foreach (var name in required)
        if (!options.ContainsKey(name))
            throw new ArgumentException($"Missing --{name}.");
    return options;
}
