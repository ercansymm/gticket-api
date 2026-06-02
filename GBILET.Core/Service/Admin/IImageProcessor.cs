namespace GBILET.Core.Service.Admin;

public interface IImageProcessor
{
    /// <summary>
    /// Decodes the uploaded image, downscales it to <paramref name="maxWidth"/> when wider
    /// (aspect ratio preserved), and re-encodes it as compressed WebP.
    /// </summary>
    /// <returns>Processed image bytes and the file extension to store (e.g. ".webp").</returns>
    Task<(byte[] Data, string Extension)> ProcessAsync(
        Stream input, int maxWidth, int quality, CancellationToken ct = default);
}
