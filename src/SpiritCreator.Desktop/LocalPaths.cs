using Microsoft.Win32;

namespace SpiritCreator.Desktop;

internal static class LocalPaths
{
    internal static string SaveRoot { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EldenRing");

    internal static string NewExportFolder() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SpiritCreator", "Exports", Guid.NewGuid().ToString("N"));

    internal static string? FindStockRegulation()
    {
        if (!OperatingSystem.IsWindows())
            return null;
        using var steam = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
        var prefix = steam?.GetValue("SteamPath") as string;
        return string.IsNullOrWhiteSpace(prefix) ? null : Existing(Path.Combine(prefix, "steamapps",
            "common", "ELDEN RING", "Game", "regulation.bin"));
    }

    internal static string? FindMe3()
    {
        var candidate = Existing(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs", "garyttierney", "me3", "bin", "me3.exe"));
        if (candidate is not null)
            return candidate;
        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var path = Existing(Path.Combine(directory.Trim('"'), "me3.exe"));
            if (path is not null && Path.IsPathFullyQualified(path))
                return path;
        }
        return null;
    }

    private static string? Existing(string path) => File.Exists(path) ? path : null;
}
