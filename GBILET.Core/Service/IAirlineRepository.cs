using GBILET.Core.Entities;

namespace GBILET.Core.Service;

public interface IAirlineRepository
{
    Task<List<Airline>> GetAllAsync();
    Task<Airline?> GetByCodeAsync(string code);
    Task<Dictionary<string, string>> GetAirlineDictionaryAsync();
}
