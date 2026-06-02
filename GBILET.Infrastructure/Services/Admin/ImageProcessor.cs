using GBILET.Core.Service.Admin;
using SkiaSharp;

namespace GBILET.Infrastructure.Services.Admin;

/// <summary>
/// SkiaSharp-based image processor. Resizes oversized uploads and re-encodes them as
/// compressed WebP so blog pages ship lightweight images instead of multi-MB originals.
/// </summary>
public class ImageProcessor : IImageProcessor
{
    public async Task<(byte[] Data, string Extension)> ProcessAsync(
        Stream input, int maxWidth, int quality, CancellationToken ct = default)
    {
        // SkiaSharp decoding is synchronous and needs a seekable stream, so buffer first.
        using var buffer = new MemoryStream();
        await input.CopyToAsync(buffer, ct);
        buffer.Position = 0;

        using var original = SKBitmap.Decode(buffer);
        if (original is null)
            throw new InvalidOperationException("Görsel çözümlenemedi.");

        SKBitmap? resized = null;
        var source = original;

        if (original.Width > maxWidth)
        {
            var ratio = (float)maxWidth / original.Width;
            var newHeight = (int)Math.Round(original.Height * ratio);
            resized = original.Resize(new SKImageInfo(maxWidth, newHeight), SKFilterQuality.High);
            if (resized is not null)
                source = resized;
        }

        try
        {
            using var image = SKImage.FromBitmap(source);
            using var data = image.Encode(SKEncodedImageFormat.Webp, quality);
            if (data is null)
                throw new InvalidOperationException("Görsel kodlanamadı.");

            return (data.ToArray(), ".webp");
        }
        finally
        {
            resized?.Dispose();
        }
    }
}
