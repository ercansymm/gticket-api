// Airline Logo Kontrol — Tek seferlik çalıştırılacak kontrol kodu
// Program.cs'e geçici olarak app.Run()'dan ÖNCE ekleyin, çalıştırıp kaldırın.
//
// ─── KULLANIM ───
// Program.cs içinde app.Run(); satırından ÖNCE şu satırı ekleyin:
//   await AirlineLogoChecker.CheckAsync(app);
// Sonra:
//   dotnet run --project GBILET.Api
// Console çıktısını okuyun, ardından satırı kaldırın.

using GBILET.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public static class AirlineLogoChecker
{
    public static async Task CheckAsync(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GTicketDbContext>();
        var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();

        var logoDir = Path.Combine(env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"),
                                   "images", "airlines");

        Console.WriteLine("\n===== AIRLINE LOGO KONTROL =====");

        // 1. Lokal dosyalar
        Console.WriteLine($"\n[1] Lokal logo klasoru: {logoDir}");
        var localCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (Directory.Exists(logoDir))
        {
            foreach (var f in Directory.GetFiles(logoDir, "*.png"))
            {
                var code = Path.GetFileNameWithoutExtension(f).ToUpperInvariant();
                localCodes.Add(code);
                Console.WriteLine($"  {code}.png");
            }
            Console.WriteLine($"  Toplam: {localCodes.Count} logo");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("  KLASOR YOK!");
            Console.ResetColor();
        }

        // 2. DB'deki airline code'lar
        Console.WriteLine("\n[2] DB'deki TUM airline'lar:");
        var airlines = await db.Airlines
            .OrderBy(a => a.Code)
            .Select(a => new { a.Code, a.NameTr, a.LogoUrl, a.IsActive })
            .ToListAsync();

        foreach (var a in airlines)
            Console.WriteLine($"  {a.Code} - {a.NameTr} (LogoUrl: {a.LogoUrl ?? "NULL"}) {(a.IsActive ? "[AKTIF]" : "[PASIF]")}");

        Console.WriteLine($"  Toplam: {airlines.Count} airline ({airlines.Count(a => a.IsActive)} aktif, {airlines.Count(a => !a.IsActive)} pasif)");

        // 3. Karşılaştırma
        Console.WriteLine("\n[3] KARSILASTIRMA:");
        var dbCodes = airlines.Select(a => a.Code.ToUpperInvariant()).ToHashSet();

        var logosuOlan = dbCodes.Where(c => localCodes.Contains(c)).OrderBy(c => c).ToList();
        var logosuEksik = dbCodes.Where(c => !localCodes.Contains(c)).OrderBy(c => c).ToList();
        var fazlaLogo = localCodes.Where(c => !dbCodes.Contains(c)).OrderBy(c => c).ToList();

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\n  LOGOSU OLAN:");
        foreach (var c in logosuOlan) Console.WriteLine($"    [OK] {c}");
        if (logosuOlan.Count == 0) Console.WriteLine("    (yok)");

        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("\n  LOGOSU EKSIK:");
        foreach (var c in logosuEksik) Console.WriteLine($"    [EKSIK] {c}");
        if (logosuEksik.Count == 0) Console.WriteLine("    (hepsi mevcut)");

        if (fazlaLogo.Count > 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n  DB'de OLMAYAN ama lokalde OLAN:");
            foreach (var c in fazlaLogo) Console.WriteLine($"    [FAZLA] {c}");
        }

        Console.ResetColor();
        Console.WriteLine($"\n===== OZET =====");
        Console.WriteLine($"  DB aktif airline:  {dbCodes.Count}");
        Console.WriteLine($"  Lokal logo:        {localCodes.Count}");
        Console.WriteLine($"  Logosu olan:        {logosuOlan.Count}");
        Console.WriteLine($"  Logosu eksik:       {logosuEksik.Count}\n");
    }
}
