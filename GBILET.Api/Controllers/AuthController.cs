using GBILET.Core.DTOs.Auth;
using GBILET.Core.Service.Auth;
using Microsoft.AspNetCore.Mvc;

namespace GBILET.Api.Controllers
{
    [Route("api/auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpGet("test")]
        public IActionResult Test() => Ok("API çalışıyor");

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
        {
            var (success, error, user) = await _authService.RegisterAsync(request, ct);
            if (!success)
                return BadRequest(new { error });
            return Ok(user);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
        {
            var (success, error, user) = await _authService.LoginAsync(request, ct);
            if (!success)
                return Unauthorized(new { error });
            return Ok(user);
        }

        [HttpPost("google-login")]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request, CancellationToken ct)
        {
            var (success, error, user) = await _authService.GoogleLoginAsync(request, ct);
            if (!success)
                return BadRequest(new { error });
            return Ok(user);
        }
    }
}
