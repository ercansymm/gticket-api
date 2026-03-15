using GBILET.Core.Entities;
using GBILET.Core.Service;
using Microsoft.EntityFrameworkCore;

namespace GBILET.Infrastructure.Data;

public class PopularRouteRepository : IPopularRouteRepository
{
    private readonly GTicketDbContext _db;

    public PopularRouteRepository(GTicketDbContext db)
    {
        _db = db;
    }

    public async Task<List<PopularRoute>> GetAllAsync()
    {
        return await _db.PopularRoutes
            .Where(r => r.IsActive)
            .OrderBy(r => r.SortOrder)
            .ToListAsync();
    }
}
