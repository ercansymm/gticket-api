using GBILET.Core.Models.Flight;


namespace GBILET.Core.Service.Flight;

public interface IFlightService
{
    Task<AirSearchResponse> SearchFlightAsync(SearchRequest request);
    Task<FlightSearchResponseDto> SearchFlightDtoAsync(SearchRequest request);
    Task<AllocateResponse> AllocateFlightAsync(AllocateRequest request);
    Task<UpdatePassengersResponse> UpdatePassengersAsync(UpdatePassengersRequest request);
    Task<MakePreBookingResponse> MakePreBookingAsync(MakePreBookingRequest request);
}