using System.Security.Claims;
using GBILET.Core.DTOs.Support;
using GBILET.Core.Service.Support;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GBILET.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/support-tickets")]
[Authorize(AuthenticationSchemes = "AdminBearer")]
public class AdminSupportTicketsController : ControllerBase
{
    private readonly ISupportTicketService _support;
    private readonly ILogger<AdminSupportTicketsController> _logger;

    public AdminSupportTicketsController(
        ISupportTicketService support,
        ILogger<AdminSupportTicketsController> logger)
    {
        _support = support;
        _logger = logger;
    }

    // ============================================================
    // GET /api/admin/support-tickets
    // Tüm ticket'lar (sayfalama + filtre)
    // ============================================================
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] AdminSupportTicketFilterRequest filter,
        CancellationToken ct)
    {
        var result = await _support.GetAllForAdminAsync(filter, ct);
        return Ok(result);
    }

    // ============================================================
    // GET /api/admin/support-tickets/{id}
    // Ticket detayı + mesajlar
    // ============================================================
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetDetail(Guid id, CancellationToken ct)
    {
        var result = await _support.GetDetailForAdminAsync(id, ct);
        if (result == null)
            return NotFound(new { error = "Destek talebi bulunamadı." });

        return Ok(result);
    }

    // ============================================================
    // POST /api/admin/support-tickets/{id}/messages
    // Admin talebe mesaj ekler
    // ============================================================
    [HttpPost("{id:guid}/messages")]
    public async Task<IActionResult> AddMessage(
        Guid id,
        [FromBody] AddSupportMessageRequest request,
        CancellationToken ct)
    {
        var adminId = GetAdminUserId();
        if (adminId == null)
            return Unauthorized(new { error = "Admin oturumu bulunamadı." });

        try
        {
            var result = await _support.AddMessageByAdminAsync(id, adminId.Value, request, ct);
            return Ok(result);
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
    // PATCH /api/admin/support-tickets/{id}/close
    // Ticket'ı kapat
    // ============================================================
    [HttpPatch("{id:guid}/close")]
    public async Task<IActionResult> Close(Guid id, CancellationToken ct)
    {
        var adminId = GetAdminUserId();
        if (adminId == null)
            return Unauthorized(new { error = "Admin oturumu bulunamadı." });

        try
        {
            var result = await _support.CloseByAdminAsync(id, adminId.Value, ct);
            return Ok(result);
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
    // Helpers
    // ============================================================
    private Guid? GetAdminUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? User.FindFirst("sub")?.Value
                    ?? User.FindFirst("adminUserId")?.Value;

        if (Guid.TryParse(claim, out var id))
            return id;
        return null;
    }
}