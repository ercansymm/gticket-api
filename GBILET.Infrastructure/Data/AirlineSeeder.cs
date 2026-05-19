using GBILET.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GBILET.Infrastructure.Data;

public class AirlineSeeder
{
    private readonly GTicketDbContext _db;
    private readonly ILogger<AirlineSeeder> _logger;

    public AirlineSeeder(GTicketDbContext db, ILogger<AirlineSeeder> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        var existingCount = await _db.Airlines.CountAsync();
        if (existingCount > 0)
        {
            _logger.LogInformation("Havayolları zaten yüklenmiş ({Count} kayıt). Seed atlanıyor.", existingCount);
            return;
        }

        _logger.LogInformation("Havayolları seed ediliyor...");

        var airlines = new List<Airline>
        {
            new() { Code = "TK", NameTr = "Türk Hava Yolları", NameEn = "Turkish Airlines", CountryCode = "TR", Alliance = "Star Alliance", IsDomestic = true, IsActive = true, SortOrder = 1, LogoUrl = "/images/airlines/tk.png", CreatedAt = DateTime.UtcNow },
            new() { Code = "PC", NameTr = "Pegasus Hava Yolları", NameEn = "Pegasus Airlines", CountryCode = "TR", Alliance = null, IsDomestic = true, IsActive = true, SortOrder = 2, LogoUrl = "/images/airlines/pc.png", CreatedAt = DateTime.UtcNow },
            new() { Code = "VF", NameTr = "AnadoluJet", NameEn = "AnadoluJet", CountryCode = "TR", Alliance = null, IsDomestic = true, IsActive = true, SortOrder = 3, LogoUrl = "/images/airlines/vf.png", CreatedAt = DateTime.UtcNow },
            new() { Code = "XQ", NameTr = "SunExpress", NameEn = "SunExpress", CountryCode = "TR", Alliance = null, IsDomestic = true, IsActive = true, SortOrder = 4, LogoUrl = "/images/airlines/xq.png", CreatedAt = DateTime.UtcNow },
            new() { Code = "XC", NameTr = "Corendon Airlines", NameEn = "Corendon Airlines", CountryCode = "TR", Alliance = null, IsDomestic = true, IsActive = true, SortOrder = 5, LogoUrl = "/images/airlines/xc.png", CreatedAt = DateTime.UtcNow },
            new() { Code = "LH", NameTr = "Lufthansa", NameEn = "Lufthansa", CountryCode = "DE", Alliance = "Star Alliance", IsDomestic = false, IsActive = true, SortOrder = 10, LogoUrl = "/images/airlines/lh.png", CreatedAt = DateTime.UtcNow },
            new() { Code = "BA", NameTr = "British Airways", NameEn = "British Airways", CountryCode = "GB", Alliance = "Oneworld", IsDomestic = false, IsActive = true, SortOrder = 11, LogoUrl = "/images/airlines/ba.png", CreatedAt = DateTime.UtcNow },
            new() { Code = "AF", NameTr = "Air France", NameEn = "Air France", CountryCode = "FR", Alliance = "SkyTeam", IsDomestic = false, IsActive = true, SortOrder = 12, LogoUrl = "/images/airlines/af.png", CreatedAt = DateTime.UtcNow },
            new() { Code = "EK", NameTr = "Emirates", NameEn = "Emirates", CountryCode = "AE", Alliance = null, IsDomestic = false, IsActive = true, SortOrder = 13, LogoUrl = "/images/airlines/ek.png", CreatedAt = DateTime.UtcNow },
            new() { Code = "QR", NameTr = "Qatar Airways", NameEn = "Qatar Airways", CountryCode = "QA", Alliance = "Oneworld", IsDomestic = false, IsActive = true, SortOrder = 14, LogoUrl = "/images/airlines/qr.png", CreatedAt = DateTime.UtcNow },
        };

        _db.Airlines.AddRange(airlines);
        await _db.SaveChangesAsync();

        _logger.LogInformation("{Count} havayolu başarıyla seed edildi.", airlines.Count);
    }
}
