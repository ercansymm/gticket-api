using GBILET.Core.Service;
using GBILET.Core.Service;
using Microsoft.AspNetCore.Mvc;

namespace GBILET.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AirportController : ControllerBase
{
    private readonly IAirportRepository _airportRepository;

    public AirportController(IAirportRepository airportRepository)
    {
        _airportRepository = airportRepository;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var airports = await _airportRepository.GetAllAsync(page, pageSize);
        var total = await _airportRepository.GetTotalCountAsync();
        return Ok(new
        {
            success = true,
            data = airports,
            pagination = new
            {
                page,
                pageSize,
                totalCount = total,
                totalPages = (int)Math.Ceiling(total / (double)pageSize)
            }
        });
    }

    [HttpGet("domestic")]
    public async Task<IActionResult> GetDomestic()
    {
        var airports = await _airportRepository.GetDomesticAsync();
        return Ok(new { success = true, data = airports, count = airports.Count });
    }

    [HttpGet("international")]
    public async Task<IActionResult> GetInternational()
    {
        var airports = await _airportRepository.GetInternationalAsync();
        return Ok(new { success = true, data = airports, count = airports.Count });
    }

    [HttpGet("popular")]
    public async Task<IActionResult> GetPopular()
    {
        var airports = await _airportRepository.GetPopularAsync();
        return Ok(new { success = true, data = airports, count = airports.Count });
    }

    [HttpGet("country/{countryCode}")]
    public async Task<IActionResult> GetByCountry(string countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode) || countryCode.Length != 2)
            return BadRequest(new
            {
                success = false,
                error = new { code = "INVALID_COUNTRY_CODE", message = "Ülke kodu 2 harfli ISO 3166-1 alpha-2 formatında olmalıdır." }
            });

        var airports = await _airportRepository.GetByCountryAsync(countryCode);
        return Ok(new { success = true, data = airports, count = airports.Count });
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] int limit = 10)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
            return BadRequest(new
            {
                success = false,
                error = new { code = "QUERY_TOO_SHORT", message = "En az 2 karakter giriniz." }
            });

        var results = await _airportRepository.SearchAsync(q, limit);
        return Ok(new { success = true, data = results, count = results.Count });
    }

    [HttpGet("{iataCode}")]
    public async Task<IActionResult> GetByCode(string iataCode)
    {
        if (string.IsNullOrWhiteSpace(iataCode) || iataCode.Length > 4)
            return BadRequest(new
            {
                success = false,
                error = new { code = "INVALID_IATA_CODE", message = "Geçerli bir IATA kodu giriniz." }
            });

        var airport = await _airportRepository.GetByIataCodeAsync(iataCode);
        if (airport == null)
            return NotFound(new
            {
                success = false,
                error = new { code = "AIRPORT_NOT_FOUND", message = $"'{iataCode.ToUpperInvariant()}' kodlu havalimanı bulunamadı." }
            });

        return Ok(new { success = true, data = airport });
    }
}
