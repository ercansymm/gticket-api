using GBILET.Core.Entities;
using GBILET.Core.Service;
using Microsoft.EntityFrameworkCore;

namespace GBILET.Infrastructure.Data;

public class AirlineRepository : IAirlineRepository
{
    private readonly GTicketDbContext _db;

    public AirlineRepository(GTicketDbContext db)
    {
        _db = db;
    }

    public async Task<List<Airline>> GetAllAsync()
    {
        return await _db.Airlines
            .Where(a => a.IsActive)
            .OrderBy(a => a.SortOrder)
            .ToListAsync();
    }

    public async Task<Airline?> GetByCodeAsync(string code)
    {
        return await _db.Airlines
            .FirstOrDefaultAsync(a => a.Code == code);
    }

    public async Task<Dictionary<string, string>> GetAirlineDictionaryAsync()
    {
        return await _db.Airlines
            .Where(a => a.IsActive)
            .ToDictionaryAsync(a => a.Code, a => a.NameTr);
    }
}
