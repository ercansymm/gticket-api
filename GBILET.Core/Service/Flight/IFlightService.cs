using GBILET.Core.Models.Flight;


namespace GBILET.Core.Service.Flight;

public interface IFlightService
{
    Task<AirSearchResponse> SearchFlightAsync(SearchRequest request);
    Task<FlightSearchResponseDto> SearchFlightDtoAsync(SearchRequest request);
    Task<AllocateResponse> AllocateFlightAsync(AllocateRequest request);
    Task<UpdatePassengersResponse> UpdatePassengersAsync(UpdatePassengersRequest request);
    Task<MakePreBookingResponse> MakePreBookingAsync(MakePreBookingRequest request);
    Task<RemoveProductResponse> RemoveProductAsync(RemoveProductRequest request);
    Task<MakePaymentResponse> MakePaymentAsync(MakePaymentRequest request);
    Task<FinalizeShoppingResponse> FinalizeShoppingAsync(FinalizeShoppingRequest request);
    Task<PokeShoppingFileResponse> PokeShoppingFileAsync(PokeShoppingFileRequest request);
    Task<ReadShoppingFileResponse> ReadShoppingFileAsync(ReadShoppingFileRequest request);
    Task<LogoutResponse> LogoutAsync(LogoutRequest request);
}