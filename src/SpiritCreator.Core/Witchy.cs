using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace SpiritCreator;

internal sealed class Witchy(string executable)
{
    internal static Witchy CreatePrivateCopy(string original, string destination)
    {
        original = Path.GetFullPath(original);
        if (!File.Exists(original) || !string.Equals(Path.GetFileName(original), "WitchyBND.exe", StringComparison.OrdinalIgnoreCase))
            throw new FileNotFoundException("Pass the extracted WitchyBND 3.0.1.0 WitchyBND.exe.", original);
        var version = FileVersionInfo.GetVersionInfo(original);
        if ((version.FileMajorPart, version.FileMinorPart, version.FileBuildPart, version.FilePrivatePart) != (3, 0, 1, 0))
            throw new InvalidDataException($"Expected WitchyBND 3.0.1.0, found {version.FileVersion}.");
        var source = Path.GetDirectoryName(original)!;
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source))
        {
            var name = Path.GetFileName(file);
            if (name.Equals("WitchyBND.exe", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("appsettings.json", StringComparison.OrdinalIgnoreCase) ||
                Path.GetExtension(file).Equals(".dll", StringComparison.OrdinalIgnoreCase))
                File.Copy(file, Path.Combine(destination, name));
        }
        var assets = Path.Combine(source, "Assets");
        if (!Directory.Exists(assets))
            throw new DirectoryNotFoundException("Extract the full WitchyBND release, including Assets.");
        CopyTree(assets, Path.Combine(destination, "Assets"));
        var paramdex = Path.Combine(destination, "Assets", "Paramdex");
        if (!Directory.Exists(paramdex))
            ZipFile.ExtractToDirectory(Path.Combine(destination, "Assets", "Paramdex.zip"), paramdex);
        var ceiling = Path.Combine(paramdex, "ER", "Upgrader", "version.txt");
        if (!File.Exists(ceiling))
            throw new FileNotFoundException("WitchyBND is missing its ER Paramdex version file.", ceiling);
        // The exporter pins the exact verified stock hash. Change only the tool copy's ceiling.
        File.WriteAllText(ceiling, "1_17_3_0034L");
        File.WriteAllText(Path.Combine(destination, "appsettings.override.json"), """
            {
              "Offline": true,
              "Bnd": true,
              "Dcx": false,
              "Recursive": false,
              "ParamCellStyle": "Attribute"
            }
            """);
        return new Witchy(Path.Combine(destination, "WitchyBND.exe"));
    }

    internal void Run(string path, string operation)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
            throw new FileNotFoundException($"Missing input to {operation}.", path);
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(executable)!
        };
        start.ArgumentList.Add("--passive");
        start.ArgumentList.Add(path);
        using var process = Process.Start(start) ?? throw new IOException("Could not start WitchyBND.");
        if (!process.WaitForExit(90_000))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            throw new TimeoutException($"WitchyBND took more than 90 seconds to {operation}.");
        }
        if (process.ExitCode != 0)
            throw new IOException($"WitchyBND could not {operation} (exit {process.ExitCode}).");
    }

    private static void CopyTree(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var file in Directory.EnumerateFiles(from))
            File.Copy(file, Path.Combine(to, Path.GetFileName(file)));
        foreach (var dir in Directory.EnumerateDirectories(from))
            CopyTree(dir, Path.Combine(to, Path.GetFileName(dir)));
    }
}

internal sealed class ScratchDirectory(string path) : IDisposable
{
    internal string Path { get; } = path;
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Warning: could not clean up temporary folder {Path}: {ex.Message}");
        }
    }
}

internal static class ScratchExtensions
{
    internal static ScratchDirectory AsScratch(this DirectoryInfo directory) => new(directory.FullName);
}
