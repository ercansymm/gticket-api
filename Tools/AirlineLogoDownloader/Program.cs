// Airline Logo Downloader — Tek seferlik çalıştırılacak tool
// Kullanım: dotnet run --project Tools/AirlineLogoDownloader
//
// wwwroot/images/airlines/ klasörüne logo indirir.
// Kaynak 1: airhex.com (200x200)
// Kaynak 2: pics.avs.io (fallback)

using var httpClient = new HttpClient();
httpClient.Timeout = TimeSpan.FromSeconds(15);
httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");

var codes = new[] { "TK", "PC", "VF", "XQ", "XC", "LH", "BA", "AF", "EK", "QR" };

// wwwroot/images/airlines/ klasörünü bul/oluştur
var apiProjectDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "GBILET.Api"));
var outputDir = Path.Combine(apiProjectDir, "wwwroot", "images", "airlines");
Directory.CreateDirectory(outputDir);

Console.WriteLine($"Çıktı klasörü: {outputDir}\n");

int success = 0, failed = 0;

foreach (var code in codes)
{
    var fileName = $"{code.ToLowerInvariant()}.png";
    var filePath = Path.Combine(outputDir, fileName);

    // Kaynak 1: airhex
    var airhexUrl = $"https://content.airhex.com/content/logos/airlines_{code}_200_200_s.png";
    if (await TryDownloadAsync(httpClient, airhexUrl, filePath, code, "airhex"))
    {
        success++;
        continue;
    }

    // Kaynak 2: avs.io
    var avsUrl = $"https://pics.avs.io/200/200/{code}.png";
    if (await TryDownloadAsync(httpClient, avsUrl, filePath, code, "avs.io"))
    {
        success++;
        continue;
    }

    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"  [{code}] BASARISIZ — her iki kaynaktan da indirilemedi");
    Console.ResetColor();
    failed++;
}

Console.WriteLine($"\n===== OZET =====");
Console.WriteLine($"  Basarili: {success}");
Console.WriteLine($"  Basarisiz: {failed}");
Console.WriteLine($"  Toplam: {codes.Length}");

static async Task<bool> TryDownloadAsync(HttpClient client, string url, string filePath, string code, string source)
{
    try
    {
        var response = await client.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  [{code}] {source}: HTTP {(int)response.StatusCode}");
            Console.ResetColor();
            return false;
        }

        var bytes = await response.Content.ReadAsByteArrayAsync();

        // Minimum boyut kontrolü — çok küçükse muhtemelen hatalı/boş
        if (bytes.Length < 500)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  [{code}] {source}: dosya cok kucuk ({bytes.Length} byte), atlanıyor");
            Console.ResetColor();
            return false;
        }

        await File.WriteAllBytesAsync(filePath, bytes);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  [{code}] {source}: OK ({bytes.Length / 1024.0:F1} KB) -> {Path.GetFileName(filePath)}");
        Console.ResetColor();
        return true;
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"  [{code}] {source}: HATA — {ex.Message}");
        Console.ResetColor();
        return false;
    }
}
