using GBILET.Core.Entities;
using GBILET.Core.Service;
using Microsoft.EntityFrameworkCore;

namespace GBILET.Infrastructure.Data;

public class AirportRepository : IAirportRepository
{
    private readonly GTicketDbContext _db;

    public AirportRepository(GTicketDbContext db)
    {
        _db = db;
    }

    public async Task<List<Airport>> GetAllAsync()
    {
        return await _db.Airports
            .Where(a => a.IsActive)
            .OrderBy(a => a.SortOrder)
            .ToListAsync();
    }

    public async Task<List<Airport>> GetDomesticAsync()
    {
        return await _db.Airports
            .Where(a => a.IsActive && a.IsDomestic)
            .OrderBy(a => a.SortOrder)
            .ToListAsync();
    }

    public async Task<Airport?> GetByIataCodeAsync(string iataCode)
    {
        return await _db.Airports
            .FirstOrDefaultAsync(a => a.IataCode == iataCode);
    }

    public async Task<List<Airport>> SearchAsync(string query)
    {
        var q = query.ToLower();
        return await _db.Airports
            .Where(a => a.IsActive &&
                (a.IataCode.ToLower().Contains(q) ||
                 a.NameTr.ToLower().Contains(q) ||
                 a.NameEn.ToLower().Contains(q) ||
                 a.CityTr.ToLower().Contains(q) ||
                 a.CityEn.ToLower().Contains(q)))
            .OrderBy(a => a.SortOrder)
            .Take(10)
            .ToListAsync();
    }
}
