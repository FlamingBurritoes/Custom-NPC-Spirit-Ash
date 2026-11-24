using SkiaSharp;

namespace SpiritCreator.Desktop;

internal sealed record PortraitImage(byte[] Rgba, byte[] PreviewPng)
{
    internal static PortraitImage Load(string path)
    {
        using var file = File.OpenRead(path);
        if (file.Length is <= 0 or > 32 * 1024 * 1024)
            throw new InvalidDataException("Choose a PNG or JPEG under 32 MB.");
        using var codec = SKCodec.Create(file);
        if (codec is null || codec.EncodedFormat is not (SKEncodedImageFormat.Png or SKEncodedImageFormat.Jpeg) ||
            codec.Info.Width is <= 0 or > 4096 || codec.Info.Height is <= 0 or > 4096)
            throw new InvalidDataException("Choose a PNG or JPEG no larger than 4096 × 4096 pixels.");
        using var source = new SKBitmap(codec.Info.WithColorType(SKColorType.Rgba8888).WithAlphaType(SKAlphaType.Premul));
        if (codec.GetPixels(source.Info, source.GetPixels()) != SKCodecResult.Success)
            throw new InvalidDataException("Could not decode the portrait.");
        const int size = SummonPortrait.Size;
        using var square = new SKBitmap(size, size, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(square))
        using (var paint = new SKPaint { FilterQuality = SKFilterQuality.High })
        {
            canvas.Clear(SKColors.Transparent);
            var side = Math.Min(source.Width, source.Height);
            float x = (source.Width - side) / 2f, y = (source.Height - side) / 2f;
            canvas.DrawBitmap(source, new SKRect(x, y, x + side, y + side), new SKRect(0, 0, size, size), paint);
        }
        var rgba = new byte[SummonPortrait.RgbaBytes];
        var at = 0;
        foreach (var color in square.Pixels)
        {
            rgba[at++] = color.Red;
            rgba[at++] = color.Green;
            rgba[at++] = color.Blue;
            rgba[at++] = color.Alpha;
        }
        using var png = square.Encode(SKEncodedImageFormat.Png, 100);
        return new(rgba, png.ToArray());
    }
}
