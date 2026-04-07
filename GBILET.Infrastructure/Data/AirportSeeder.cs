using System.Text.Json;
using GBILET.Core.Entities;
using GBILET.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GBILET.Infrastructure.Data;

public class AirportSeeder
{
    private readonly GTicketDbContext _db;
    private readonly ILogger<AirportSeeder> _logger;

    public AirportSeeder(GTicketDbContext db, ILogger<AirportSeeder> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// airports_seed.json dosyasını okur ve veritabanına yükler.
    /// Eğer havalimanları zaten varsa, tekrar yüklemez.
    /// </summary>
    public async Task SeedAsync()
    {
        // Eğer zaten 100'den fazla havalimanı varsa, seed'e gerek yok
        var existingCount = await _db.Airports.CountAsync();
        if (existingCount > 100)
        {
            _logger.LogInformation("Havalimanları zaten yüklenmiş ({Count} kayıt). Seed atlanıyor.", existingCount);
            return;
        }

        var jsonPath = FindSeedFile();
        if (jsonPath == null)
        {
            _logger.LogWarning("airports_seed.json bulunamadı! Havalimanı seed'i atlanıyor.");
            return;
        }

        _logger.LogInformation("Havalimanları yükleniyor: {Path}", jsonPath);

        try
        {
            var json = await File.ReadAllTextAsync(jsonPath);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var seedAirports = JsonSerializer.Deserialize<List<AirportSeedDto>>(json, options);
            if (seedAirports == null || seedAirports.Count == 0)
            {
                _logger.LogWarning("airports_seed.json boş veya okunamadı.");
                return;
            }

            // Mevcut IATA kodlarını al (çakışma olmasın)
            var existingCodes = await _db.Airports
                .Select(a => a.IataCode)
                .ToListAsync();
            var existingSet = new HashSet<string>(existingCodes, StringComparer.OrdinalIgnoreCase);

            var newAirports = new List<Airport>();
            foreach (var dto in seedAirports)
            {
                if (string.IsNullOrWhiteSpace(dto.IataCode) || existingSet.Contains(dto.IataCode))
                    continue;

                existingSet.Add(dto.IataCode);

                newAirports.Add(new Airport
                {
                    IataCode = dto.IataCode.ToUpperInvariant(),
                    IcaoCode = dto.IcaoCode,
                    NameTr = dto.NameTr ?? dto.NameEn ?? "",
                    NameEn = dto.NameEn ?? "",
                    CityTr = dto.CityTr ?? dto.CityEn ?? "",
                    CityEn = dto.CityEn ?? "",
                    CountryTr = dto.CountryTr ?? dto.CountryEn ?? "",
                    CountryEn = dto.CountryEn ?? "",
                    CountryCode = dto.CountryCode ?? "",
                    Timezone = dto.Timezone,
                    Latitude = dto.Latitude,
                    Longitude = dto.Longitude,
                    CityCode = dto.CityCode??
                        (CityCodeMap.TryGetValue(dto.IataCode, out var cc) ? cc : dto.IataCode.ToUpperInvariant()),
                    Type = dto.Type ?? "airport",
                    IsDomestic = dto.IsDomestic,
                    IsPopular = dto.IsPopular,
                    IsActive = dto.IsActive,
                    SortOrder = dto.SortOrder,
                    CreatedAt = DateTime.UtcNow
                });
            }

            if (newAirports.Count > 0)
            {
                // Batch insert (performans için)
                const int batchSize = 500;
                for (int i = 0; i < newAirports.Count; i += batchSize)
                {
                    var batch = newAirports.Skip(i).Take(batchSize).ToList();
                    await _db.Airports.AddRangeAsync(batch);
                    await _db.SaveChangesAsync();
                    _logger.LogInformation("Havalimanı batch yüklendi: {Current}/{Total}",
                        Math.Min(i + batchSize, newAirports.Count), newAirports.Count);
                }

                _logger.LogInformation("Toplam {Count} havalimanı başarıyla yüklendi.", newAirports.Count);
            }
            else
            {
                _logger.LogInformation("Yüklenecek yeni havalimanı bulunamadı.");
            }

            // Mevcut kayıtlarda CityCode null olanları güncelle
            await UpdateMissingCityCodesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Havalimanı seed hatası!");
            throw;
        }
    }

    /// <summary>
    /// CityCode null olan mevcut kayıtları CityCodeMap'e göre günceller.
    /// </summary>
    private async Task UpdateMissingCityCodesAsync()
    {
        var toUpdate = await _db.Airports
            .Where(a => a.CityCode == null)
            .ToListAsync();

        if (toUpdate.Count == 0) return;

        foreach (var airport in toUpdate)
        {
            airport.CityCode = CityCodeMap.TryGetValue(airport.IataCode, out var cc)
                ? cc
                : airport.IataCode;
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation("{Count} havalimanı için CityCode güncellendi.", toUpdate.Count);
    }

    /// <summary>
    /// airports_seed.json dosyasını bulmaya çalışır.
    /// Sırasıyla: Data/ klasörü, proje kökü, çalışma dizini
    /// </summary>
    private string? FindSeedFile()
    {
        var candidates = new[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "airports_seed.json"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "airports_seed.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "Data", "airports_seed.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "airports_seed.json"),
            // Geliştirme ortamı için
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Data", "airports_seed.json"),
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
                return path;
        }

        return null;
    }

    /// <summary>
    /// IATA havalimanı kodu → şehir kodu eşleşmesi.
    /// Seed JSON'da cityCode yoksa buradan alınır.
    /// </summary>
    private static readonly Dictionary<string, string> CityCodeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // İstanbul — hem IST hem SAW aynı şehir, city code = IST
        { "IST", "IST" },
        { "SAW", "IST" },
        // Ankara — city code = ANK
        { "ESB", "ANK" },
        { "ANK", "ANK" },
        // İzmir — city code = IZM
        { "ADB", "IZM" },
        // Londra
        { "LHR", "LON" }, { "LGW", "LON" }, { "STN", "LON" }, { "LTN", "LON" }, { "LCY", "LON" },
        // Paris
        { "CDG", "PAR" }, { "ORY", "PAR" },
        // New York
        { "JFK", "NYC" }, { "LGA", "NYC" }, { "EWR", "NYC" },
        // Tokyo
        { "NRT", "TYO" }, { "HND", "TYO" },
        // Stockholm
        { "ARN", "STO" }, { "BMA", "STO" },
        // Milan
        { "MXP", "MIL" }, { "LIN", "MIL" },
        // Chicago
        { "ORD", "CHI" }, { "MDW", "CHI" },
    };

    /// <summary>
    /// JSON dosyasından okumak için DTO sınıfı
    /// </summary>
    private class AirportSeedDto
    {
        public string IataCode { get; set; } = "";
        public string? IcaoCode { get; set; }
        public string? NameEn { get; set; }
        public string? NameTr { get; set; }
        public string? CityEn { get; set; }
        public string? CityTr { get; set; }
        public string? CountryEn { get; set; }
        public string? CountryTr { get; set; }
        public string? CountryCode { get; set; }
        public string? Timezone { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? CityCode { get; set; }
        public string? Type { get; set; }
        public bool IsDomestic { get; set; }
        public bool IsPopular { get; set; }
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; }
    }
}