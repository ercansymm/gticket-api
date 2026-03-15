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
    public async Task<IActionResult> GetAll()
    {
        var airports = await _airportRepository.GetAllAsync();
        return Ok(airports);
    }

    [HttpGet("domestic")]
    public async Task<IActionResult> GetDomestic()
    {
        var airports = await _airportRepository.GetDomesticAsync();
        return Ok(airports);
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
            return BadRequest(new { error = "En az 2 karakter giriniz." });

        var results = await _airportRepository.SearchAsync(q);
        return Ok(results);
    }

    [HttpGet("{iataCode}")]
    public async Task<IActionResult> GetByCode(string iataCode)
    {
        var airport = await _airportRepository.GetByIataCodeAsync(iataCode);
        if (airport == null)
            return NotFound(new { error = $"'{iataCode}' kodlu havalimaný bulunamadý." });

        return Ok(airport);
    }
}
