using GBILET.Core.Models.Flight;
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

    [HttpPost("search")]
    public async Task<IActionResult> Search([FromBody] SearchRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Origin) || string.IsNullOrWhiteSpace(request.Destination))
                return BadRequest(new { error = "Origin ve Destination alanları zorunludur." });

            if (request.DepartureDate == default)
                return BadRequest(new { error = "DepartureDate alanı zorunludur." });

            if (request.FlightType == "RT" && !request.ReturnDate.HasValue)
                return BadRequest(new { error = "Gidiş-dönüş uçuşlar için ReturnDate zorunludur." });

            if (request.AdultCount + request.ChildCount > 9)
                return BadRequest(new { error = "Bebek hariç toplam yolcu sayısı 9'u geçemez." });

            if (request.InfantCount > request.AdultCount)
                return BadRequest(new { error = "Bebek sayısı yetişkin sayısını geçemez." });

            var result = await _flightService.SearchFlightAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                error = ex.Message,
                inner = ex.InnerException?.Message
            });
        }
    }

    [HttpPost("allocate")]
    public async Task<IActionResult> Allocate([FromBody] AllocateRequest? request)
    {
        try
        {
            if (request == null)
                return BadRequest(new { error = "Request body parse edilemedi. JSON formatını kontrol edin." });

            if (request.SearchRequest == null)
                return BadRequest(new { error = "SearchRequest alanı zorunludur." });

            if (string.IsNullOrWhiteSpace(request.ProductId))
                return BadRequest(new { error = "ProductId alanı zorunludur. Search sonucundan bir FlightOption.ProductId seçin." });

            var result = await _flightService.AllocateFlightAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                error = ex.Message,
                inner = ex.InnerException?.Message
            });
        }
    }
}