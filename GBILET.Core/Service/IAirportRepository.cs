using GBILET.Core.Entities;

using GBILET.Core.Entities;

namespace GBILET.Core.Service;

public interface IAirportRepository
{
    Task<List<Airport>> GetAllAsync(int page = 1, int pageSize = 50);
    Task<int> GetTotalCountAsync();
    Task<List<Airport>> GetDomesticAsync();
    Task<List<Airport>> GetInternationalAsync();
    Task<List<Airport>> GetPopularAsync();
    Task<List<Airport>> GetByCountryAsync(string countryCode);
    Task<Airport?> GetByIataCodeAsync(string iataCode);
    Task<List<Airport>> SearchAsync(string query, int limit = 10);
}
