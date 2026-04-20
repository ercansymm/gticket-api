using GBILET.Core.Service.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GBILET.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/customers")]
[Authorize]
[EnableRateLimiting("admin-general")]
public class AdminCustomersController : ControllerBase
{
    private readonly IAdminCustomerService _customerService;

    public AdminCustomersController(IAdminCustomerService customerService)
    {
        _customerService = customerService;
    }

    [HttpGet("passengers")]
    public async Task<IActionResult> GetPassengers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var result = await _customerService.GetPassengersAsync(page, pageSize, search, ct);
        return Ok(result);
    }

    [HttpGet("booking-contacts")]
    public async Task<IActionResult> GetBookingContacts(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var result = await _customerService.GetBookingContactsAsync(page, pageSize, search, ct);
        return Ok(result);
    }
}
