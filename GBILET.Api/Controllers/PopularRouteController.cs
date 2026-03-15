using GBILET.Core.Service;
using Microsoft.AspNetCore.Mvc;

namespace GBILET.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PopularRouteController : ControllerBase
{
    private readonly IPopularRouteRepository _popularRouteRepository;

    public PopularRouteController(IPopularRouteRepository popularRouteRepository)
    {
        _popularRouteRepository = popularRouteRepository;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var routes = await _popularRouteRepository.GetAllAsync();
        return Ok(routes);
    }
}
