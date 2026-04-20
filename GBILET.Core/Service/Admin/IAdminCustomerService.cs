using GBILET.Core.DTOs.Admin;

namespace GBILET.Core.Service.Admin;

public interface IAdminCustomerService
{
    Task<CustomerListResponseDto> GetPassengersAsync(int page, int pageSize, string? search, CancellationToken ct);
    Task<CustomerListResponseDto> GetBookingContactsAsync(int page, int pageSize, string? search, CancellationToken ct);
}
