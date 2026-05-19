using GBILET.Core.DTOs.Support;
using GBILET.Core.Service.Support;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GBILET.Api.Controllers;

/// <summary>
/// Misafir (üye olmayan) destek talebi akışı.
/// Erişim PNR + Soyad ile doğrulanır; sonraki istekler kısa ömürlü guest token ile yapılır.
/// Token DataProtection ile şifrelenmiş, BookingId'ye kapsamlı (başka rezervasyona erişim verilemez).
/// </summary>
[ApiController]
[Route("api/support/guest")]
[AllowAnonymous]
public class SupportGuestController : ControllerBase
{
    private const string GuestTokenHeader = "X-Guest-Support-Token";

    private readonly ISupportTicketService _support;
    private readonly IGuestSupportTokenService _tokenService;
    private readonly ILogger<SupportGuestController> _logger;

    public SupportGuestController(
        ISupportTicketService support,
        IGuestSupportTokenService tokenService,
        ILogger<SupportGuestController> logger)
    {
        _support = support;
        _tokenService = tokenService;
        _logger = logger;
    }

    // ============================================================
    // POST /api/support/guest/lookup
    // PNR + Soyad doğrulaması; başarılı ise 30 dk geçerli token döner.
    // Hata mesajı kasıtlı olarak generic — enumerasyon engellenir.
    // ============================================================
    [HttpPost("lookup")]
    [EnableRateLimiting("guest-support-lookup")]
    public async Task<IActionResult> Lookup(
        [FromBody] GuestSupportLookupRequest request,
        CancellationToken ct)
    {
        if (request == null
            || string.IsNullOrWhiteSpace(request.Pnr)
            || string.IsNullOrWhiteSpace(request.Surname))
        {
            return BadRequest(new { error = "PNR ve soyad zorunludur." });
        }

        // Basit format ön doğrulaması — sunucuya geçersiz değer atılmadan kesilir.
        var pnr = request.Pnr.Trim();
        if (pnr.Length < 5 || pnr.Length > 10)
        {
            // Generic mesaj — formatın yanlış olduğunu belirtme
            return Unauthorized(new { error = "PNR veya soyad hatalı." });
        }

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        var result = await _support.LookupGuestBookingAsync(request.Pnr, request.Surname, ct);
        if (result == null)
        {
            _logger.LogInformation(
                "Guest support lookup FAILED ip={Ip} pnr={Pnr}",
                ip, pnr);
            // Generic — PNR mi soyad mı yanlış belli olmasın
            return Unauthorized(new { error = "PNR veya soyad hatalı." });
        }

        var (bookingId, displayName) = result.Value;
        var (token, expiresAt) = _tokenService.Issue(bookingId, TimeSpan.FromMinutes(30));

        var ticketCount = (await _support.GetByGuestBookingAsync(bookingId, ct)).Count;

        _logger.LogInformation(
            "Guest support lookup OK ip={Ip} pnr={Pnr} bookingId={BookingId}",
            ip, pnr, bookingId);

        return Ok(new GuestSupportLookupResponse
        {
            Token = token,
            ExpiresAt = expiresAt,
            BookingId = bookingId,
            Pnr = pnr.ToUpperInvariant(),
            PassengerDisplayName = displayName,
            TicketCount = ticketCount
        });
    }

    // ============================================================
    // GET /api/support/guest/tickets
    // ============================================================
    [HttpGet("tickets")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var (bookingId, _) = ResolveTokenOrFail();
        if (bookingId == null) return UnauthorizedToken();

        var items = await _support.GetByGuestBookingAsync(bookingId.Value, ct);
        return Ok(items);
    }

    // ============================================================
    // POST /api/support/guest/tickets
    // ============================================================
    [HttpPost("tickets")]
    public async Task<IActionResult> Create(
        [FromBody] GuestCreateSupportTicketRequest request,
        [FromHeader(Name = "X-Guest-Display-Name")] string? displayName,
        CancellationToken ct)
    {
        var (bookingId, _) = ResolveTokenOrFail();
        if (bookingId == null) return UnauthorizedToken();

        try
        {
            var result = await _support.CreateByGuestAsync(
                bookingId.Value,
                displayName ?? "Misafir",
                request,
                ct);
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
            _logger.LogError(ex, "Error creating guest support ticket for booking {BookingId}", bookingId);
            return StatusCode(500, new { error = "Talep oluşturulurken bir hata oluştu." });
        }
    }

    // ============================================================
    // GET /api/support/guest/tickets/{id}
    // ============================================================
    [HttpGet("tickets/{id:guid}")]
    public async Task<IActionResult> Detail(Guid id, CancellationToken ct)
    {
        var (bookingId, _) = ResolveTokenOrFail();
        if (bookingId == null) return UnauthorizedToken();

        var detail = await _support.GetDetailForGuestAsync(id, bookingId.Value, ct);
        if (detail == null)
            return NotFound(new { error = "Destek talebi bulunamadı." });

        return Ok(detail);
    }

    // ============================================================
    // POST /api/support/guest/tickets/{id}/messages
    // ============================================================
    [HttpPost("tickets/{id:guid}/messages")]
    public async Task<IActionResult> AddMessage(
        Guid id,
        [FromBody] AddSupportMessageRequest request,
        [FromHeader(Name = "X-Guest-Display-Name")] string? displayName,
        CancellationToken ct)
    {
        var (bookingId, _) = ResolveTokenOrFail();
        if (bookingId == null) return UnauthorizedToken();

        try
        {
            var result = await _support.AddMessageByGuestAsync(
                id,
                bookingId.Value,
                displayName ?? "Misafir",
                request,
                ct);
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
            _logger.LogError(ex, "Error adding guest message to ticket {TicketId}", id);
            return StatusCode(500, new { error = "Mesaj gönderilemedi." });
        }
    }

    // ============================================================
    // Helpers
    // ============================================================

    private (Guid? BookingId, string? Token) ResolveTokenOrFail()
    {
        if (!Request.Headers.TryGetValue(GuestTokenHeader, out var tokenValues))
            return (null, null);

        var token = tokenValues.ToString();
        if (string.IsNullOrWhiteSpace(token))
            return (null, null);

        var bookingId = _tokenService.Validate(token);
        return (bookingId, token);
    }

    private IActionResult UnauthorizedToken() =>
        Unauthorized(new { error = "Oturum süresi dolmuş veya geçersiz. Lütfen PNR + soyad ile yeniden giriş yapın." });
}
