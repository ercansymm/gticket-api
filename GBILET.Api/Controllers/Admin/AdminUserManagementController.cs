using System.Security.Claims;
using GBILET.Core.DTOs.Admin;
using GBILET.Core.Service.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GBILET.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = "SuperAdmin")]
public class AdminUserManagementController : ControllerBase
{
    private readonly IAdminUserManagementService _service;
    private readonly ILogger<AdminUserManagementController> _logger;

    public AdminUserManagementController(
        IAdminUserManagementService service,
        ILogger<AdminUserManagementController> logger)
    {
        _service = service;
        _logger = logger;
    }

    // ============================================================
    // GET /api/admin/users
    // ============================================================
    [HttpGet]
    [EnableRateLimiting("admin-general")]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var users = await _service.GetAllAsync(ct);
        return Ok(users);
    }





            // ============================================================
    // GET /api/admin/users/{id}
    // ============================================================
    [HttpGet("{id:guid}")]
    [EnableRateLimiting("admin-general")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var user = await _service.GetByIdAsync(id, ct);
        if (user == null)
            return NotFound(new { error = "Admin kullanıcı bulunamadı." });

        return Ok(user);
    }

    // ============================================================
    // POST /api/admin/users
    // ============================================================
    [HttpPost]
    [EnableRateLimiting("admin-general")]
    public async Task<IActionResult> Create([FromBody] CreateAdminUserRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            var firstError = ModelState.Values
                .SelectMany(v => v.Errors)
                .FirstOrDefault()?.ErrorMessage ?? "Geçersiz veri.";
            return BadRequest(new { error = firstError });
        }

        var performedBy = GetUserIdFromClaims();
        if (performedBy == null) return Unauthorized();

        try
        {
            var user = await _service.CreateAsync(
                request, performedBy.Value, GetIp(), GetUserAgent(), ct);
            return Ok(user);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // ============================================================
    // PATCH /api/admin/users/{id}/status
    // ============================================================
    [HttpPatch("{id:guid}/status")]
    [EnableRateLimiting("admin-general")]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        [FromBody] UpdateAdminStatusRequest request,
        CancellationToken ct)
    {
        if (request == null)
            return BadRequest(new { error = "Geçersiz istek." });

        var performedBy = GetUserIdFromClaims();
        if (performedBy == null) return Unauthorized();

        try
        {
            var user = await _service.UpdateStatusAsync(
                id, request.IsActive, performedBy.Value, GetIp(), GetUserAgent(), ct);
            return Ok(user);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // ============================================================
    // POST /api/admin/users/{id}/reset-password
    // ============================================================
    [HttpPost("{id:guid}/reset-password")]
    [EnableRateLimiting("admin-general")]
    public async Task<IActionResult> ResetPassword(
        Guid id,
        [FromBody] ResetAdminPasswordRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            var firstError = ModelState.Values
                .SelectMany(v => v.Errors)
                .FirstOrDefault()?.ErrorMessage ?? "Geçersiz veri.";
            return BadRequest(new { error = firstError });
        }

        var performedBy = GetUserIdFromClaims();
        if (performedBy == null) return Unauthorized();

        try
        {
            await _service.ResetPasswordAsync(
                id, request.NewPassword, performedBy.Value, GetIp(), GetUserAgent(), ct);
            return Ok(new { message = "Parola başarıyla sıfırlandı." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    // ============================================================
    // Helpers
    // ============================================================
    private Guid? GetUserIdFromClaims()
    {
        var idStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(idStr, out var id) ? id : null;
    }

    private string? GetIp() =>
        Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
        ?? HttpContext.Connection.RemoteIpAddress?.ToString();

    private string? GetUserAgent() =>
        Request.Headers.UserAgent.ToString();
}