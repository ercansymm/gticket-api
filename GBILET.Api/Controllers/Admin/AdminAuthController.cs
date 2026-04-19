using System.Security.Claims;
using GBILET.Core.DTOs.Admin;
using GBILET.Core.Service.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace GBILET.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/auth")]
public class AdminAuthController : ControllerBase
{
    private readonly IAdminAuthService _auth;
    private readonly JwtOptions _jwtOptions;
    private readonly ILogger<AdminAuthController> _logger;

    private const string AccessCookieName = "atabilet_access_token";
    private const string RefreshCookieName = "atabilet_refresh_token";

    public AdminAuthController(
        IAdminAuthService auth,
        IOptions<JwtOptions> jwtOptions,
        ILogger<AdminAuthController> logger)
    {
        _auth = auth;
        _jwtOptions = jwtOptions.Value;
        _logger = logger;
    }

    // ============================================================
    // POST /api/admin/auth/login  (Step 1)
    // ============================================================
    [HttpPost("login")]
    [EnableRateLimiting("admin-login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] AdminLoginRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "Kullanıcı adı ve parola gerekli." });

        try
        {
            var (response, tokens) = await _auth.LoginAsync(request, GetIp(), GetUserAgent());

            if (tokens != null)
            {
                // 2FA gerekmedi — token'ları cookie'lere koy
                SetTokenCookies(tokens);
            }

            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { error = ex.Message });
        }
    }

    // ============================================================
    // POST /api/admin/auth/verify-2fa  (Step 2)
    // ============================================================
    [HttpPost("verify-2fa")]
    [EnableRateLimiting("admin-login")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyTwoFactor([FromBody] AdminVerify2FaRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.TwoFactorToken) || string.IsNullOrWhiteSpace(request.Code))
            return BadRequest(new { error = "Token ve kod gerekli." });

        try
        {
            var (response, tokens) = await _auth.VerifyTwoFactorAsync(request, GetIp(), GetUserAgent());
            if (tokens != null) SetTokenCookies(tokens);
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { error = ex.Message });
        }
    }

    // ============================================================
    // POST /api/admin/auth/refresh
    // ============================================================
    [HttpPost("refresh")]
    [EnableRateLimiting("admin-general")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh()
    {
        if (!Request.Cookies.TryGetValue(RefreshCookieName, out var refreshToken) || string.IsNullOrEmpty(refreshToken))
        {
            ClearTokenCookies();
            return Unauthorized(new { error = "Refresh token bulunamadı." });
        }

        var tokens = await _auth.RefreshAsync(refreshToken, GetIp(), GetUserAgent());
        if (tokens == null)
        {
            ClearTokenCookies();
            return Unauthorized(new { error = "Geçersiz veya süresi geçmiş refresh token." });
        }

        SetTokenCookies(tokens);
        return Ok(new { accessTokenExpiresAt = tokens.AccessTokenExpiresAt });
    }

    // ============================================================
    // POST /api/admin/auth/logout
    // ============================================================
    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout()
    {
        if (Request.Cookies.TryGetValue(RefreshCookieName, out var refreshToken) && !string.IsNullOrEmpty(refreshToken))
        {
            await _auth.LogoutAsync(refreshToken, GetIp());
        }
        ClearTokenCookies();
        return Ok(new { message = "Çıkış yapıldı." });
    }

    // ============================================================
    // GET /api/admin/auth/me
    // ============================================================
    [HttpGet("me")]
    [Authorize]
    [EnableRateLimiting("admin-general")]
    public IActionResult Me()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var username = User.FindFirst(ClaimTypes.Name)?.Value;
        var email = User.FindFirst(ClaimTypes.Email)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        var fullName = User.FindFirst("fullName")?.Value;

        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        return Ok(new
        {
            id = userId,
            username,
            email,
            fullName,
            role
        });
    }

    // ============================================================
    // POST /api/admin/auth/setup-2fa
    // ============================================================
    [HttpPost("setup-2fa")]
    [Authorize]
    [EnableRateLimiting("admin-general")]
    public async Task<IActionResult> SetupTwoFactor()
    {
        var userId = GetUserIdFromClaims();
        if (userId == null) return Unauthorized();

        try
        {
            var response = await _auth.InitiateTwoFactorSetupAsync(userId.Value);
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { error = ex.Message });
        }
    }

    // ============================================================
    // POST /api/admin/auth/confirm-2fa
    // ============================================================
    [HttpPost("confirm-2fa")]
    [Authorize]
    [EnableRateLimiting("admin-general")]
    public async Task<IActionResult> ConfirmTwoFactor([FromBody] AdminSetup2FaRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Code))
            return BadRequest(new { error = "Doğrulama kodu gerekli." });

        var userId = GetUserIdFromClaims();
        if (userId == null) return Unauthorized();

        try
        {
            var success = await _auth.ConfirmTwoFactorSetupAsync(userId.Value, request.Code);
            if (!success) return BadRequest(new { error = "Doğrulama kodu hatalı." });

            return Ok(new { message = "2FA başarıyla aktifleştirildi." });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { error = ex.Message });
        }
    }

    // ============================================================
    // Helpers
    // ============================================================

    private void SetTokenCookies(AdminTokens tokens)
    {
        var isHttps = Request.IsHttps;

        var accessOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = isHttps,                // Production'da HTTPS zorunlu
            SameSite = SameSiteMode.Strict,
            Path = "/",
            Expires = tokens.AccessTokenExpiresAt
        };
        Response.Cookies.Append(AccessCookieName, tokens.AccessToken, accessOptions);

        var refreshOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = isHttps,
            SameSite = SameSiteMode.Strict,
            Path = "/api/admin/auth",        // Sadece auth endpoint'lerine gönderilsin
            Expires = tokens.RefreshTokenExpiresAt
        };
        Response.Cookies.Append(RefreshCookieName, tokens.RefreshToken, refreshOptions);
    }

    private void ClearTokenCookies()
    {
        var isHttps = Request.IsHttps;
        var common = new CookieOptions
        {
            HttpOnly = true,
            Secure = isHttps,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UnixEpoch
        };
        Response.Cookies.Append(AccessCookieName, "", new CookieOptions
        {
            HttpOnly = true, Secure = isHttps, SameSite = SameSiteMode.Strict,
            Path = "/", Expires = DateTimeOffset.UnixEpoch
        });
        Response.Cookies.Append(RefreshCookieName, "", new CookieOptions
        {
            HttpOnly = true, Secure = isHttps, SameSite = SameSiteMode.Strict,
            Path = "/api/admin/auth", Expires = DateTimeOffset.UnixEpoch
        });
    }

    private string? GetIp() =>
        Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
        ?? HttpContext.Connection.RemoteIpAddress?.ToString();

    private string? GetUserAgent() =>
        Request.Headers.UserAgent.ToString();

    private Guid? GetUserIdFromClaims()
    {
        var idStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(idStr, out var id) ? id : null;
    }
}
