using GBILET.Core.Service.Flight;
using Microsoft.AspNetCore.Mvc;

namespace GBILET.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FlightController : ControllerBase
{
    private readonly IFlightService _flightService;

    public FlightController(IFlightService flightService)
    {
        _flightService = flightService;
    }
    [HttpGet("search")]
    public async Task<IActionResult> Search(string from, string to)
    {
        try
        {
            var result = await _flightService.SearchFlight(from, to);
            return Content(result, "text/xml");
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                error = ex.Message,
                inner = ex.InnerException?.Message,
                stack = ex.StackTrace
            });
        }
    }
}