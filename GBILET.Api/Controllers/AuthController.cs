using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GBILET.Api.Controllers
{
    [Route("api/auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        [HttpGet("test")]
        public IActionResult Test()
        {
            return Ok("API çalışıyor");
        }
    }
}
