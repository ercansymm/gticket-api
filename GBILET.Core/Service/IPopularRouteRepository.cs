using GBILET.Core.Entities;

namespace GBILET.Core.Service;

public interface IPopularRouteRepository
{
    Task<List<PopularRoute>> GetAllAsync();
}
