using GBILET.Core.Service.Email;
using Microsoft.AspNetCore.Mvc;

namespace GBILET.Api.Controllers;

/// <summary>
/// Geçici e-posta şablon test endpoint'i.
/// X-API-Key middleware tarafından korunur — sadece yetkili çağrılar geçer.
/// </summary>
[ApiController]
[Route("api/email-test")]
public class EmailTestController : ControllerBase
{
    private readonly IEmailService? _email;
    private readonly ILogger<EmailTestController> _logger;

    public EmailTestController(ILogger<EmailTestController> logger, IEmailService? email = null)
    {
        _logger = logger;
        _email = email;
    }

    /// <summary>
    /// POST /api/email-test/booking-confirmation
    /// Body: { "toEmail": "ornek@gmail.com" }
    /// </summary>
    [HttpPost("booking-confirmation")]
    public async Task<IActionResult> SendBookingConfirmation([FromBody] EmailTestRequest req, CancellationToken ct)
    {
        if (_email is null)
            return StatusCode(503, new { error = "Email service not configured." });

        if (string.IsNullOrWhiteSpace(req.ToEmail))
            return BadRequest(new { error = "toEmail is required." });

        var flights = new List<BookingEmailFlight>
        {
            new("ADB", "SAW", "TK 2301", "Turkish Airlines",
                DateTime.UtcNow.AddDays(7), "06:45", "08:00", "20 kg"),
            new("SAW", "ADB", "TK 2302", "Turkish Airlines",
                DateTime.UtcNow.AddDays(14), "18:30", "19:50", "20 kg"),
        };

        var passengers = new List<BookingEmailPassenger>
        {
            new("ERCAN YILMAZ", "ADT", "235-1234567890", "12345678901", "+90 532 000 00 00"),
            new("AYŞE YILMAZ",  "ADT", "235-0987654321", "98765432100", "+90 533 000 00 00"),
        };

        await _email.SendBookingConfirmationAsync(
            toEmail:        req.ToEmail,
            toName:         req.ToName ?? "Test Kullanıcı",
            pnr:            "TESTPNR",
            bookingId:      Guid.NewGuid(),
            flights:        flights,
            passengers:     passengers,
            grandTotal:     3_450.00m,
            currency:       "TRY",
            pdfDownloadUrl: "https://atabilet.com/bilet-sorgula",
            ct:             ct
        );

        _logger.LogInformation("[EmailTest] Booking confirmation sent to {Email}", req.ToEmail);
        return Ok(new { message = $"Booking confirmation e-postası {req.ToEmail} adresine gönderildi." });
    }

    /// <summary>
    /// POST /api/email-test/support-created
    /// Body: { "toEmail": "ornek@gmail.com" }
    /// </summary>
    [HttpPost("support-created")]
    public async Task<IActionResult> SendSupportCreated([FromBody] EmailTestRequest req, CancellationToken ct)
    {
        if (_email is null)
            return StatusCode(503, new { error = "Email service not configured." });

        if (string.IsNullOrWhiteSpace(req.ToEmail))
            return BadRequest(new { error = "toEmail is required." });

        await _email.SendTicketCreatedNotificationAsync(
            toEmail:      req.ToEmail,
            toName:       req.ToName ?? "Test Kullanıcı",
            ticketNumber: "DST-2025-0042",
            subject:      "İade talebi hakkında",
            ticketId:     Guid.NewGuid(),
            ct:           ct
        );

        _logger.LogInformation("[EmailTest] Support-created sent to {Email}", req.ToEmail);
        return Ok(new { message = $"Destek talebi açıldı e-postası {req.ToEmail} adresine gönderildi." });
    }

    /// <summary>
    /// POST /api/email-test/support-closed
    /// Body: { "toEmail": "ornek@gmail.com" }
    /// </summary>
    [HttpPost("support-closed")]
    public async Task<IActionResult> SendSupportClosed([FromBody] EmailTestRequest req, CancellationToken ct)
    {
        if (_email is null)
            return StatusCode(503, new { error = "Email service not configured." });

        if (string.IsNullOrWhiteSpace(req.ToEmail))
            return BadRequest(new { error = "toEmail is required." });

        await _email.SendTicketClosedNotificationAsync(
            toEmail:      req.ToEmail,
            toName:       req.ToName ?? "Test Kullanıcı",
            ticketNumber: "DST-2025-0042",
            subject:      "İade talebi hakkında",
            ticketId:     Guid.NewGuid(),
            ct:           ct
        );

        _logger.LogInformation("[EmailTest] Support-closed sent to {Email}", req.ToEmail);
        return Ok(new { message = $"Destek kapatıldı e-postası {req.ToEmail} adresine gönderildi." });
    }
}

public record EmailTestRequest(string ToEmail, string? ToName = null);
