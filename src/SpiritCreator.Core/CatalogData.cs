using System.Globalization;

namespace SpiritCreator;

internal static class CatalogData
{
    internal static int Int(string value) => int.Parse(value, CultureInfo.InvariantCulture);

    internal static IEnumerable<string[]> Read(string name)
    {
        using var stream = typeof(CatalogData).Assembly.GetManifestResourceStream($"SpiritCreator.Core.Catalog.{name}.tsv")
            ?? throw new InvalidDataException($"Missing catalog: {name}.");
        using var reader = new StreamReader(stream);
        while (reader.ReadLine() is { Length: > 0 } line)
            yield return line.Split('\t');
    }
}
