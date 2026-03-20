using GBILET.Core.Entities;
using GBILET.Core.Helpers;
using GBILET.Core.Service;
using Microsoft.AspNetCore.Mvc;

namespace GBILET.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AirlineController : ControllerBase
{
    private readonly IAirlineRepository _airlineRepository;

    public AirlineController(IAirlineRepository airlineRepository)
    {
        _airlineRepository = airlineRepository;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var airlines = await _airlineRepository.GetAllAsync();
        return Ok(airlines);
    }

    [HttpGet("{code}")]
    public async Task<IActionResult> GetByCode(string code)
    {
        var airline = await _airlineRepository.GetByCodeAsync(code);
        if (airline == null)
            return NotFound(new { error = $"'{code}' kodlu havayolu bulunamadı." });

        return Ok(airline);
    }
}
