using GBILET.Core.Models.Flight;

namespace GBILET.Core.Service.Flight;

public interface IFlightService
{
    Task<AirSearchResponse> SearchFlightAsync(SearchRequest request);
}