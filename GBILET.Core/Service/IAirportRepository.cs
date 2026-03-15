using GBILET.Core.Entities;

namespace GBILET.Core.Service;

public interface IAirportRepository
{
    Task<List<Airport>> GetAllAsync();
    Task<List<Airport>> GetDomesticAsync();
    Task<Airport?> GetByIataCodeAsync(string iataCode);
    Task<List<Airport>> SearchAsync(string query);
}
