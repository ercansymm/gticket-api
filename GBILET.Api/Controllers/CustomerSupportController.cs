using GBILET.Core.DTOs.Support;
using GBILET.Core.Service.Support;
using Microsoft.AspNetCore.Mvc;

namespace GBILET.Api.Controllers;

// Customer-facing support endpoints. Auth handled by Next.js server-side proxy
// which validates NextAuth session and forwards user id via X-User-Id header.
[ApiController]
[Route("api/customer-support")]
public class CustomerSupportController : ControllerBase
{
    private readonly ISupportTicketService _support;
    private readonly ILogger<CustomerSupportController> _logger;

    public CustomerSupportController(
        ISupportTicketService support,
        ILogger<CustomerSupportController> logger)
    {
        _support = support;
        _logger = logger;
    }

    [HttpPost("tickets")]
    public async Task<IActionResult> Create([FromBody] CreateSupportTicketRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized(new { error = "Oturum bulunamadı." });

        try
        {
            var result = await _support.CreateByCustomerAsync(userId.Value, request, ct);
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating customer support ticket for {UserId}", userId);
            return StatusCode(500, new { error = "Talep oluşturulurken bir hata oluştu." });
        }
    }

    [HttpGet("tickets")]
    public async Task<IActionResult> GetMyTickets(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized(new { error = "Oturum bulunamadı." });

        var result = await _support.GetByCustomerAsync(userId.Value, ct);
        return Ok(result);
    }

    [HttpGet("tickets/{id:guid}")]
    public async Task<IActionResult> GetDetail(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized(new { error = "Oturum bulunamadı." });

        var result = await _support.GetDetailForCustomerAsync(id, userId.Value, ct);
        if (result == null)
            return NotFound(new { error = "Destek talebi bulunamadı." });

        return Ok(result);
    }

    [HttpPost("tickets/{id:guid}/messages")]
    public async Task<IActionResult> AddMessage(Guid id, [FromBody] AddSupportMessageRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized(new { error = "Oturum bulunamadı." });

        try
        {
            var result = await _support.AddMessageByCustomerAsync(id, userId.Value, request, ct);
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

    private Guid? GetUserId()
    {
        var header = Request.Headers["X-User-Id"].ToString();
        return Guid.TryParse(header, out var id) ? id : null;
    }
}
