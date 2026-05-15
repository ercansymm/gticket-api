using GBILET.Core.DTOs.Support;
using GBILET.Core.Entities.Support;
using GBILET.Core.Interfaces;
using GBILET.Core.Service.Email;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace GBILET.Infrastructure.Services.Email;

public class SmtpEmailService : IEmailService
{
    private readonly EmailOptions _opts;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IOptions<EmailOptions> opts, ILogger<SmtpEmailService> logger)
    {
        _opts = opts.Value;
        _logger = logger;
    }

    // ─────────────────────────────────────────────
    //  Public API
    // ─────────────────────────────────────────────

    public async Task SendTicketCreatedNotificationAsync(
        string toEmail,
        string toName,
        string ticketNumber,
        string subject,
        Guid ticketId,
        CancellationToken ct = default)
    {
        var html = BuildCreatedTemplate(toName, ticketNumber, subject, ticketId);
        await SendAsync(toEmail, toName, $"Destek Talebiniz Alındı — {ticketNumber}", html, ct);
    }

    public async Task SendSupportReplyNotificationAsync(
        string toEmail,
        string toName,
        string ticketNumber,
        string subject,
        string replyBody,
        Guid ticketId,
        IReadOnlyList<SupportTicketMessageDto> allMessages,
        CancellationToken ct = default)
    {
        var html = BuildReplyTemplate(toName, ticketNumber, subject, ticketId, allMessages);
        await SendAsync(toEmail, toName, $"Destek Talebinize Yanıt Geldi — {ticketNumber}", html, ct);
    }

    public async Task SendTicketClosedNotificationAsync(
        string toEmail,
        string toName,
        string ticketNumber,
        string subject,
        Guid ticketId,
        CancellationToken ct = default)
    {
        var html = BuildClosedTemplate(toName, ticketNumber, subject, ticketId);
        await SendAsync(toEmail, toName, $"Destek Talebiniz Kapatıldı — {ticketNumber}", html, ct);
    }

    public async Task SendBookingConfirmationAsync(
        string toEmail,
        string toName,
        string pnr,
        Guid bookingId,
        IReadOnlyList<BookingEmailFlight> flights,
        IReadOnlyList<BookingEmailPassenger> passengers,
        decimal? grandTotal,
        string? currency,
        string pdfDownloadUrl,
        CancellationToken ct = default)
    {
        var html = BuildBookingConfirmationTemplate(toName, pnr, flights, passengers, grandTotal, currency, pdfDownloadUrl);
        await SendAsync(toEmail, toName, $"Biletiniz Oluşturuldu — {pnr}", html, ct);
    }

    public async Task SendBookingUpdatedAsync(
        string toEmail,
        string toName,
        string pnr,
        Guid bookingId,
        IReadOnlyList<BookingFieldChange> changes,
        string pdfDownloadUrl,
        CancellationToken ct = default)
    {
        var subject = $"Rezervasyonunuz Güncellendi — PNR: {pnr}";
        var html = BuildBookingUpdatedHtml(toName, pnr, changes, pdfDownloadUrl);
        await SendAsync(toEmail, toName, subject, html, ct);
    }

    // ─────────────────────────────────────────────
    //  Send
    // ─────────────────────────────────────────────

    private async Task SendAsync(string toEmail, string toName, string subject, string htmlBody, CancellationToken ct)
    {
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_opts.FromName, _opts.FromEmail));
            message.To.Add(new MailboxAddress(toName, toEmail));
            message.Subject = subject;
            message.Body = new TextPart(MimeKit.Text.TextFormat.Html) { Text = htmlBody };

            var sslOption = _opts.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;

            using var client = new SmtpClient();
            client.ServerCertificateValidationCallback = (_, _, _, _) => true;

            await client.ConnectAsync(_opts.Host, _opts.Port, sslOption, ct);
            await client.AuthenticateAsync(_opts.Username, _opts.Password, ct);
            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);

            _logger.LogInformation("Email sent to {ToEmail} — {Subject}", toEmail, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Email sending failed to {ToEmail} with subject {Subject}", toEmail, subject);
        }
    }

    // ─────────────────────────────────────────────
    //  Shared layout helpers
    // ─────────────────────────────────────────────

    private static string BuildEmailHeader(string subtitle) => $"""
        <tr>
          <td style="background:#0a1628;padding:28px 32px 24px;">
            <h1 style="margin:0;color:#ffffff;font-size:24px;font-weight:700;letter-spacing:0.3px;font-family:Arial,sans-serif;">
              ATA<span style="color:#db2525;">BİLET</span>
            </h1>
            <p style="margin:6px 0 0;color:#cbd5e1;font-size:13px;font-weight:500;font-family:Arial,sans-serif;">{System.Net.WebUtility.HtmlEncode(subtitle)}</p>
          </td>
        </tr>
        """;

    private static string BuildEmailFooter() => $"""
        <tr>
          <td style="background:#f1f5f9;padding:22px 32px;border-top:1px solid #e2e8f0;">
            <p style="margin:0 0 4px;color:#94a3b8;font-size:12px;text-align:center;font-family:Arial,sans-serif;">
              Bu e-posta otomatik olarak gönderilmiştir. Lütfen bu adrese doğrudan yanıt vermeyiniz.
            </p>
            <p style="margin:0;color:#cbd5e1;font-size:12px;text-align:center;font-family:Arial,sans-serif;">
              &#169; {DateTime.UtcNow.Year} Atabilet.com &mdash; Tüm hakları saklıdır.
            </p>
          </td>
        </tr>
        """;

    private static string WrapInLayout(string bodyRows) => $"""
        <!DOCTYPE html>
        <html lang="tr">
        <head><meta charset="UTF-8"><meta name="viewport" content="width=device-width, initial-scale=1.0"></head>
        <body style="margin:0;padding:0;background:#f0f4f8;font-family:Arial,sans-serif;">
          <table width="100%" cellpadding="0" cellspacing="0" style="background:#f0f4f8;padding:32px 16px;">
            <tr><td align="center">
              <table width="600" cellpadding="0" cellspacing="0"
                     style="background:#ffffff;border-radius:10px;overflow:hidden;max-width:600px;box-shadow:0 2px 8px rgba(0,0,0,0.08);">
                {bodyRows}
              </table>
            </td></tr>
          </table>
        </body>
        </html>
        """;

    // ─────────────────────────────────────────────
    //  Booking confirmation
    // ─────────────────────────────────────────────

    private static string BuildBookingConfirmationTemplate(
        string name,
        string pnr,
        IReadOnlyList<BookingEmailFlight> flights,
        IReadOnlyList<BookingEmailPassenger> passengers,
        decimal? grandTotal,
        string? currency,
        string pdfDownloadUrl)
    {
        var flightsHtml = BuildFlightsHtml(flights);
        var passengersHtml = BuildPassengersHtml(passengers);
        var totalRow = grandTotal.HasValue
            ? $"""
              <tr>
                <td colspan="5" style="padding:0;">
                  <table width="100%" cellpadding="0" cellspacing="0">
                    <tr style="background:#f0fdf4;">
                      <td style="padding:14px 20px;color:#065f46;font-size:14px;font-weight:600;border-top:2px solid #d1fae5;">
                        Toplam Ödenen Tutar
                      </td>
                      <td align="right" style="padding:14px 20px;color:#0a1628;font-size:18px;font-weight:700;border-top:2px solid #d1fae5;">
                        {grandTotal.Value.ToString("N2")} {currency ?? "TRY"}
                      </td>
                    </tr>
                  </table>
                </td>
              </tr>
              """
            : string.Empty;

        var bodyRows = $"""
            {BuildEmailHeader("Uçuş Bileti")}

            <!-- Success banner -->
            <tr>
              <td style="background:#047857;padding:26px 32px;text-align:center;">
                <p style="margin:0;color:#ffffff;font-size:18px;font-weight:700;font-family:Arial,sans-serif;">
                  &#10003; Biletiniz Başarıyla Oluşturuldu
                </p>
                <p style="margin:6px 0 16px;color:#a7f3d0;font-size:13px;font-family:Arial,sans-serif;">
                  Uçuş ve yolcu bilgileriniz aşağıda yer almaktadır.
                </p>
                <div style="display:inline-block;background:#065f46;border-radius:8px;padding:12px 28px;text-align:center;">
                  <p style="margin:0;color:#6ee7b7;font-size:10px;font-weight:700;text-transform:uppercase;letter-spacing:1.5px;font-family:Arial,sans-serif;">
                    Rezervasyon Kodu
                  </p>
                  <p style="margin:5px 0 0;color:#ffffff;font-size:26px;font-weight:700;letter-spacing:5px;font-family:Arial,sans-serif;">
                    {System.Net.WebUtility.HtmlEncode(pnr)}
                  </p>
                </div>
              </td>
            </tr>

            <!-- Body -->
            <tr>
              <td style="padding:32px 32px 8px;">
                <p style="margin:0 0 8px;color:#374151;font-size:15px;font-family:Arial,sans-serif;">
                  Merhaba <strong>{System.Net.WebUtility.HtmlEncode(name)}</strong>,
                </p>
                <p style="margin:0 0 28px;color:#6b7280;font-size:14px;line-height:1.7;font-family:Arial,sans-serif;">
                  Rezervasyon işleminiz tamamlanmış ve biletiniz oluşturulmuştur.
                </p>

                <!-- Uçuş Bilgileri -->
                <p style="margin:0 0 10px;color:#0a1628;font-size:12px;font-weight:700;text-transform:uppercase;letter-spacing:1px;font-family:Arial,sans-serif;">
                  &#9992; Uçuş Bilgileri
                </p>
                <table width="100%" cellpadding="0" cellspacing="0"
                       style="border:1px solid #e5e7eb;border-radius:8px;overflow:hidden;margin-bottom:28px;">
                  {flightsHtml}
                </table>

                <!-- Yolcu Bilgileri -->
                <p style="margin:0 0 10px;color:#0a1628;font-size:12px;font-weight:700;text-transform:uppercase;letter-spacing:1px;font-family:Arial,sans-serif;">
                  &#128100; Yolcu Bilgileri
                </p>
                <table width="100%" cellpadding="0" cellspacing="0"
                       style="border:1px solid #e5e7eb;border-radius:8px;overflow:hidden;margin-bottom:28px;">
                  <tr style="background:#f8fafc;">
                    <td style="padding:10px 16px;color:#6b7280;font-size:11px;font-weight:700;text-transform:uppercase;letter-spacing:0.5px;border-bottom:1px solid #e5e7eb;font-family:Arial,sans-serif;">Yolcu</td>
                    <td style="padding:10px 16px;color:#6b7280;font-size:11px;font-weight:700;text-transform:uppercase;letter-spacing:0.5px;border-bottom:1px solid #e5e7eb;font-family:Arial,sans-serif;">TC / Pasaport</td>
                    <td style="padding:10px 16px;color:#6b7280;font-size:11px;font-weight:700;text-transform:uppercase;letter-spacing:0.5px;border-bottom:1px solid #e5e7eb;font-family:Arial,sans-serif;">Telefon</td>
                    <td style="padding:10px 16px;color:#6b7280;font-size:11px;font-weight:700;text-transform:uppercase;letter-spacing:0.5px;border-bottom:1px solid #e5e7eb;font-family:Arial,sans-serif;">Tip</td>
                    <td style="padding:10px 16px;color:#6b7280;font-size:11px;font-weight:700;text-transform:uppercase;letter-spacing:0.5px;border-bottom:1px solid #e5e7eb;font-family:Arial,sans-serif;">Bilet No</td>
                  </tr>
                  {passengersHtml}
                  {totalRow}
                </table>

                <!-- CTA -->
                <table width="100%" cellpadding="0" cellspacing="0" style="margin-bottom:10px;">
                  <tr>
                    <td align="center">
                      <a href="{pdfDownloadUrl}"
                         style="display:inline-block;background:#db2525;color:#ffffff;text-decoration:none;padding:15px 40px;border-radius:7px;font-size:15px;font-weight:700;font-family:Arial,sans-serif;letter-spacing:0.3px;">
                        &#8595; E-Bilet İndir (PDF)
                      </a>
                    </td>
                  </tr>
                </table>
                <p style="margin:8px 0 24px;color:#9ca3af;font-size:12px;text-align:center;font-family:Arial,sans-serif;">
                  E-biletinizi indirerek uçuşta ibraz edebilirsiniz.
                </p>
              </td>
            </tr>

            <!-- Info box -->
            <tr>
              <td style="padding:0 32px 32px;">
                <div style="background:#fffbeb;border:1px solid #fde68a;border-radius:8px;padding:16px 20px;">
                  <p style="margin:0 0 5px;color:#92400e;font-size:13px;font-weight:700;font-family:Arial,sans-serif;">
                    &#9888; Önemli Hatırlatma
                  </p>
                  <p style="margin:0;color:#78350f;font-size:13px;line-height:1.6;font-family:Arial,sans-serif;">
                    Uçuştan en az 2 saat önce havalimanında olunuz. Kimlik veya pasaport belgenizi yanınızda bulundurunuz.
                  </p>
                </div>
              </td>
            </tr>

            {BuildEmailFooter()}
            """;

        return WrapInLayout(bodyRows);
    }

    // ─────────────────────────────────────────────
    //  Flight & passenger row builders
    // ─────────────────────────────────────────────

    private static string BuildFlightsHtml(IReadOnlyList<BookingEmailFlight> flights)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < flights.Count; i++)
        {
            var f = flights[i];
            var isLast = i == flights.Count - 1;
            var rowBorder = isLast ? string.Empty : "border-bottom:1px solid #e5e7eb;";
            var rowBg = i % 2 == 0 ? "#ffffff" : "#f9fafb";
            var dateStr = f.DepartureDate.ToString("dd MMMM yyyy", new System.Globalization.CultureInfo("tr-TR"));
            var timeStr = !string.IsNullOrEmpty(f.DepartureTime) && !string.IsNullOrEmpty(f.ArrivalTime)
                ? $"{f.DepartureTime} &#8594; {f.ArrivalTime}"
                : f.DepartureTime ?? string.Empty;
            var baggage = !string.IsNullOrEmpty(f.Baggage)
                ? $"<span style=\"color:#6b7280;font-size:12px;\"> &middot; Bagaj: {System.Net.WebUtility.HtmlEncode(f.Baggage)}</span>"
                : string.Empty;

            sb.Append(
                $"<tr style=\"background:{rowBg};\">" +
                $"<td style=\"padding:18px 20px;{rowBorder}\">" +
                $"<table width=\"100%\" cellpadding=\"0\" cellspacing=\"0\"><tr>" +
                $"<td style=\"width:38%;\">" +
                $"<p style=\"margin:0;color:#0a1628;font-size:24px;font-weight:700;font-family:Arial,sans-serif;\">{System.Net.WebUtility.HtmlEncode(f.From)}</p>" +
                $"<p style=\"margin:3px 0 0;color:#6b7280;font-size:12px;font-family:Arial,sans-serif;\">{dateStr}</p>" +
                $"</td>" +
                $"<td align=\"center\" style=\"width:24%;\">" +
                $"<p style=\"margin:0;color:#047857;font-size:22px;font-family:Arial,sans-serif;\">&#8594;</p>" +
                $"<p style=\"margin:2px 0 0;color:#9ca3af;font-size:11px;font-family:Arial,sans-serif;\">{System.Net.WebUtility.HtmlEncode(timeStr)}</p>" +
                $"</td>" +
                $"<td style=\"width:38%;text-align:right;\">" +
                $"<p style=\"margin:0;color:#0a1628;font-size:24px;font-weight:700;font-family:Arial,sans-serif;\">{System.Net.WebUtility.HtmlEncode(f.To)}</p>" +
                $"<p style=\"margin:3px 0 0;color:#6b7280;font-size:12px;font-family:Arial,sans-serif;\">&nbsp;</p>" +
                $"</td>" +
                $"</tr></table>" +
                $"<p style=\"margin:10px 0 0;color:#374151;font-size:13px;font-family:Arial,sans-serif;\">" +
                $"{System.Net.WebUtility.HtmlEncode(f.Airline)} &middot; {System.Net.WebUtility.HtmlEncode(f.FlightNumber)}{baggage}</p>" +
                $"</td></tr>"
            );
        }
        return sb.ToString();
    }

    private static string BuildPassengersHtml(IReadOnlyList<BookingEmailPassenger> passengers)
    {
        var sb = new System.Text.StringBuilder();
        var typeMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ADT"] = "Yetişkin",
            ["CHD"] = "Çocuk",
            ["INF"] = "Bebek"
        };

        for (int i = 0; i < passengers.Count; i++)
        {
            var p = passengers[i];
            var isLast = i == passengers.Count - 1;
            var border = isLast ? string.Empty : "border-bottom:1px solid #e5e7eb;";
            var typeLabel = typeMap.TryGetValue(p.Type, out var label) ? label : p.Type;
            var ticketDisplay = !string.IsNullOrEmpty(p.TicketNumber)
                ? System.Net.WebUtility.HtmlEncode(p.TicketNumber)
                : "<span style=\"color:#cbd5e1;\">—</span>";
            var citizenDisplay = !string.IsNullOrEmpty(p.CitizenNo)
                ? System.Net.WebUtility.HtmlEncode(p.CitizenNo)
                : "<span style=\"color:#cbd5e1;\">—</span>";
            var phoneDisplay = !string.IsNullOrEmpty(p.Phone)
                ? System.Net.WebUtility.HtmlEncode(p.Phone)
                : "<span style=\"color:#cbd5e1;\">—</span>";

            sb.Append(
                $"<tr>" +
                $"<td style=\"padding:13px 16px;color:#1e293b;font-size:14px;font-weight:600;{border}font-family:Arial,sans-serif;\">{System.Net.WebUtility.HtmlEncode(p.FullName)}</td>" +
                $"<td style=\"padding:13px 16px;color:#374151;font-size:13px;font-family:monospace;{border}\">{citizenDisplay}</td>" +
                $"<td style=\"padding:13px 16px;color:#6b7280;font-size:13px;white-space:nowrap;{border}font-family:Arial,sans-serif;\">{phoneDisplay}</td>" +
                $"<td style=\"padding:13px 16px;font-size:12px;{border}font-family:Arial,sans-serif;\">" +
                $"<span style=\"background:#f0fdf4;color:#065f46;padding:3px 8px;border-radius:4px;font-weight:600;\">{typeLabel}</span></td>" +
                $"<td style=\"padding:13px 16px;color:#374151;font-size:13px;font-family:monospace;{border}\">{ticketDisplay}</td>" +
                $"</tr>"
            );
        }
        return sb.ToString();
    }

    // ─────────────────────────────────────────────
    //  Support: ticket created
    // ─────────────────────────────────────────────

    private static string BuildCreatedTemplate(string name, string ticketNumber, string subject, Guid ticketId)
    {
        var ticketUrl = $"https://atabilet.com/destek-taleplerim/{ticketId}";

        var bodyRows = $"""
            {BuildEmailHeader("Destek Merkezi")}

            <!-- Status band -->
            <tr>
              <td style="background:#0a1628;padding:18px 32px;">
                <p style="margin:0;color:rgba(255,255,255,0.85);font-size:14px;font-weight:600;font-family:Arial,sans-serif;">
                  &#128338; Talebiniz alındı — en kısa sürede dönüş yapacağız
                </p>
              </td>
            </tr>

            <!-- Body -->
            <tr>
              <td style="padding:32px;">
                <p style="margin:0 0 20px;color:#374151;font-size:15px;font-family:Arial,sans-serif;">
                  Merhaba <strong>{System.Net.WebUtility.HtmlEncode(name)}</strong>,
                </p>
                <p style="margin:0 0 24px;color:#6b7280;font-size:14px;line-height:1.7;font-family:Arial,sans-serif;">
                  Destek talebiniz başarıyla oluşturulmuştur. Destek ekibimiz en kısa sürede sizinle iletişime geçecektir.
                </p>

                <!-- Ticket card -->
                <table width="100%" cellpadding="0" cellspacing="0"
                       style="border:1px solid #e2e8f0;border-radius:8px;overflow:hidden;margin-bottom:28px;background:#f8fafc;">
                  <tr>
                    <td style="padding:20px 24px;">
                      <table width="100%" cellpadding="0" cellspacing="0">
                        <tr>
                          <td>
                            <p style="margin:0 0 12px;color:#047857;font-size:11px;font-weight:700;text-transform:uppercase;letter-spacing:1px;font-family:Arial,sans-serif;">
                              Talep Bilgileri
                            </p>
                            <table cellpadding="0" cellspacing="0">
                              <tr>
                                <td style="padding-right:12px;color:#64748b;font-size:13px;font-family:Arial,sans-serif;">Talep No</td>
                                <td style="color:#0a1628;font-size:14px;font-weight:700;font-family:Arial,sans-serif;letter-spacing:0.5px;">
                                  {System.Net.WebUtility.HtmlEncode(ticketNumber)}
                                </td>
                              </tr>
                              <tr>
                                <td style="padding:6px 12px 0 0;color:#64748b;font-size:13px;font-family:Arial,sans-serif;vertical-align:top;">Konu</td>
                                <td style="padding-top:6px;color:#374151;font-size:14px;font-family:Arial,sans-serif;">
                                  {System.Net.WebUtility.HtmlEncode(subject)}
                                </td>
                              </tr>
                            </table>
                          </td>
                        </tr>
                      </table>
                    </td>
                  </tr>
                </table>

                <table width="100%" cellpadding="0" cellspacing="0">
                  <tr>
                    <td>
                      <a href="{ticketUrl}"
                         style="display:inline-block;background:#db2525;color:#ffffff;text-decoration:none;padding:13px 30px;border-radius:7px;font-size:14px;font-weight:600;font-family:Arial,sans-serif;">
                        Talebi Görüntüle
                      </a>
                    </td>
                  </tr>
                </table>
              </td>
            </tr>

            {BuildEmailFooter()}
            """;

        return WrapInLayout(bodyRows);
    }

    // ─────────────────────────────────────────────
    //  Support: reply received
    // ─────────────────────────────────────────────

    private static string BuildReplyTemplate(
        string name,
        string ticketNumber,
        string subject,
        Guid ticketId,
        IReadOnlyList<SupportTicketMessageDto> allMessages)
    {
        var ticketUrl = $"https://atabilet.com/destek-taleplerim/{ticketId}";
        var conversationHtml = BuildConversationHtml(allMessages);

        var bodyRows = $"""
            {BuildEmailHeader("Destek Merkezi")}

            <!-- Notification band -->
            <tr>
              <td style="background:#047857;padding:18px 32px;">
                <p style="margin:0;color:#a7f3d0;font-size:14px;font-weight:600;font-family:Arial,sans-serif;">
                  &#128172; Destek ekibinizden yeni bir yanıt aldınız
                </p>
              </td>
            </tr>

            <!-- Body -->
            <tr>
              <td style="padding:32px;">
                <p style="margin:0 0 6px;color:#374151;font-size:15px;font-family:Arial,sans-serif;">
                  Merhaba <strong>{System.Net.WebUtility.HtmlEncode(name)}</strong>,
                </p>
                <p style="margin:0 0 24px;color:#6b7280;font-size:14px;font-family:Arial,sans-serif;">
                  <strong style="color:#0a1628;">{System.Net.WebUtility.HtmlEncode(ticketNumber)}</strong> numaralı
                  &ldquo;{System.Net.WebUtility.HtmlEncode(subject)}&rdquo; talebinize yanıt geldi.
                </p>

                {conversationHtml}

                <p style="margin:24px 0 20px;color:#6b7280;font-size:14px;font-family:Arial,sans-serif;">
                  Yanıtlamak veya tüm konuşmayı görmek için aşağıdaki butona tıklayın.
                </p>
                <table width="100%" cellpadding="0" cellspacing="0">
                  <tr>
                    <td>
                      <a href="{ticketUrl}"
                         style="display:inline-block;background:#047857;color:#ffffff;text-decoration:none;padding:13px 30px;border-radius:7px;font-size:14px;font-weight:600;font-family:Arial,sans-serif;">
                        Talebi Görüntüle ve Yanıtla
                      </a>
                    </td>
                  </tr>
                </table>
              </td>
            </tr>

            {BuildEmailFooter()}
            """;

        return WrapInLayout(bodyRows);
    }

    // ─────────────────────────────────────────────
    //  Support: ticket closed
    // ─────────────────────────────────────────────

    private static string BuildClosedTemplate(string name, string ticketNumber, string subject, Guid ticketId)
    {
        var ticketUrl = $"https://atabilet.com/destek-taleplerim/{ticketId}";

        var bodyRows = $"""
            {BuildEmailHeader("Destek Merkezi")}

            <!-- Resolved band -->
            <tr>
              <td style="background:#047857;padding:18px 32px;">
                <p style="margin:0;color:#ffffff;font-size:16px;font-weight:700;font-family:Arial,sans-serif;">
                  &#10003; Destek Talebiniz Çözüme Kavuşturuldu
                </p>
                <p style="margin:5px 0 0;color:#a7f3d0;font-size:13px;font-family:Arial,sans-serif;">
                  Sorununuzun giderildiğini umuyoruz.
                </p>
              </td>
            </tr>

            <!-- Body -->
            <tr>
              <td style="padding:32px;">
                <p style="margin:0 0 20px;color:#374151;font-size:15px;font-family:Arial,sans-serif;">
                  Merhaba <strong>{System.Net.WebUtility.HtmlEncode(name)}</strong>,
                </p>
                <p style="margin:0 0 24px;color:#6b7280;font-size:14px;line-height:1.7;font-family:Arial,sans-serif;">
                  <strong style="color:#0a1628;">{System.Net.WebUtility.HtmlEncode(ticketNumber)}</strong> numaralı
                  &ldquo;{System.Net.WebUtility.HtmlEncode(subject)}&rdquo; destek talebiniz çözüme kavuşturularak kapatılmıştır.
                </p>

                <!-- Info box -->
                <div style="background:#f0fdf4;border:1px solid #bbf7d0;border-radius:8px;padding:18px 20px;margin-bottom:28px;">
                  <p style="margin:0 0 5px;color:#065f46;font-size:13px;font-weight:700;font-family:Arial,sans-serif;">Sorun devam ediyor mu?</p>
                  <p style="margin:0;color:#047857;font-size:13px;line-height:1.6;font-family:Arial,sans-serif;">
                    Talebinizle ilgili ek sorunuz varsa yeni bir destek talebi oluşturabilirsiniz.
                    Ekibimiz size yardımcı olmaktan memnuniyet duyar.
                  </p>
                </div>

                <table width="100%" cellpadding="0" cellspacing="0">
                  <tr>
                    <td>
                      <a href="{ticketUrl}"
                         style="display:inline-block;background:#047857;color:#ffffff;text-decoration:none;padding:13px 30px;border-radius:7px;font-size:14px;font-weight:600;font-family:Arial,sans-serif;">
                        Talebi Görüntüle
                      </a>
                    </td>
                  </tr>
                </table>
              </td>
            </tr>

            {BuildEmailFooter()}
            """;

        return WrapInLayout(bodyRows);
    }

    // ─────────────────────────────────────────────
    //  Support: conversation history
    // ─────────────────────────────────────────────

    private static string BuildConversationHtml(IReadOnlyList<SupportTicketMessageDto> messages)
    {
        if (messages.Count == 0) return string.Empty;

        var sb = new System.Text.StringBuilder();
        sb.Append("""
            <p style="margin:0 0 12px;color:#6b7280;font-size:11px;font-weight:700;text-transform:uppercase;letter-spacing:1px;font-family:Arial,sans-serif;">
              Konuşma Geçmişi
            </p>
            """);

        foreach (var msg in messages)
        {
            var isAdmin = msg.SenderType == SupportMessageSenderType.Admin;
            var bg = isAdmin ? "#f0fdf4" : "#f8fafc";
            var borderColor = isAdmin ? "#047857" : "#94a3b8";
            var nameColor = isAdmin ? "#047857" : "#374151";
            var nameWeight = isAdmin ? "700" : "600";
            var label = isAdmin ? "Destek Ekibi" : System.Net.WebUtility.HtmlEncode(msg.SenderDisplayName);
            var time = msg.CreatedAt.ToString("dd.MM.yyyy HH:mm");
            var body = System.Net.WebUtility.HtmlEncode(msg.Body);

            sb.Append(
                $"""
                <table width="100%" cellpadding="0" cellspacing="0" style="margin-bottom:10px;">
                  <tr>
                    <td style="background:{bg};border-left:4px solid {borderColor};border-radius:0 6px 6px 0;padding:14px 18px;">
                      <table width="100%" cellpadding="0" cellspacing="0" style="margin-bottom:8px;">
                        <tr>
                          <td style="color:{nameColor};font-size:13px;font-weight:{nameWeight};font-family:Arial,sans-serif;">{label}</td>
                          <td align="right" style="color:#9ca3af;font-size:11px;white-space:nowrap;font-family:Arial,sans-serif;">{time}</td>
                        </tr>
                      </table>
                      <p style="margin:0;color:#374151;font-size:14px;line-height:1.7;white-space:pre-wrap;font-family:Arial,sans-serif;">{body}</p>
                    </td>
                  </tr>
                </table>
                """
            );
        }

        return sb.ToString();
    }

    // ─────────────────────────────────────────────
    //  Booking updated / cancelled
    // ─────────────────────────────────────────────

    private static string BuildBookingUpdatedHtml(
        string toName,
        string pnr,
        IReadOnlyList<BookingFieldChange> changes,
        string pdfDownloadUrl)
    {
        var isCancelled = changes.Any(c => c.FieldName == "Durum" && c.NewValue == "İptal");
        var headerBg = isCancelled ? "#dc2626" : "#047857";
        var headerText = isCancelled ? "&#9888; Rezervasyonunuz İptal Edildi" : "&#8635; Rezervasyonunuz Güncellendi";
        var headerSub = isCancelled
            ? "Aşağıdaki tabloda iptal detaylarını bulabilirsiniz."
            : "Rezervasyonunuzda değişiklik yapılmıştır. Detaylar aşağıda yer almaktadır.";

        var changeRows = new System.Text.StringBuilder();
        foreach (var c in changes)
        {
            changeRows.Append(
                $"<tr>" +
                $"<td style=\"padding:11px 14px;border-bottom:1px solid #e5e7eb;color:#374151;font-size:13px;font-family:Arial,sans-serif;\">{System.Net.WebUtility.HtmlEncode(c.FieldName)}</td>" +
                $"<td style=\"padding:11px 14px;border-bottom:1px solid #e5e7eb;color:#dc2626;font-size:13px;text-decoration:line-through;font-family:Arial,sans-serif;\">{System.Net.WebUtility.HtmlEncode(c.OldValue)}</td>" +
                $"<td style=\"padding:11px 14px;border-bottom:1px solid #e5e7eb;color:#047857;font-size:13px;font-weight:700;font-family:Arial,sans-serif;\">{System.Net.WebUtility.HtmlEncode(c.NewValue)}</td>" +
                $"</tr>"
            );
        }

        var ctaRow = isCancelled ? string.Empty : $"""
            <tr>
              <td align="center" style="padding-bottom:24px;">
                <a href="{pdfDownloadUrl}"
                   style="display:inline-block;background:#047857;color:#ffffff;text-decoration:none;padding:13px 32px;border-radius:7px;font-size:14px;font-weight:600;font-family:Arial,sans-serif;">
                  Güncel E-Biletinizi İndirin
                </a>
              </td>
            </tr>
            """;

        var bodyRows = $"""
            <!-- Dynamic header -->
            <tr>
              <td style="background:{headerBg};padding:26px 32px;text-align:center;">
                <p style="margin:0;color:#ffffff;font-size:20px;font-weight:700;font-family:Arial,sans-serif;">{headerText}</p>
                <p style="margin:7px 0 0;color:rgba(255,255,255,0.8);font-size:13px;font-family:Arial,sans-serif;">{headerSub}</p>
              </td>
            </tr>

            <!-- Logo strip -->
            <tr>
              <td style="background:#0a1628;padding:12px 32px;">
                <p style="margin:0;color:#ffffff;font-size:16px;font-weight:700;font-family:Arial,sans-serif;">
                  ATA<span style="color:#db2525;">BİLET</span>
                </p>
              </td>
            </tr>

            <!-- Body -->
            <tr>
              <td style="padding:32px 32px 8px;">
                <p style="margin:0 0 6px;color:#374151;font-size:15px;font-family:Arial,sans-serif;">
                  Sayın <strong>{System.Net.WebUtility.HtmlEncode(toName)}</strong>,
                </p>
                <p style="margin:0 0 24px;color:#6b7280;font-size:14px;font-family:Arial,sans-serif;">
                  PNR: <strong style="color:#0a1628;letter-spacing:1px;">{System.Net.WebUtility.HtmlEncode(pnr)}</strong>
                </p>

                <!-- Changes table -->
                <table width="100%" cellpadding="0" cellspacing="0"
                       style="border:1px solid #e5e7eb;border-radius:8px;overflow:hidden;margin-bottom:24px;">
                  <tr style="background:#f8fafc;">
                    <th style="padding:10px 14px;text-align:left;font-size:11px;color:#6b7280;font-weight:700;text-transform:uppercase;letter-spacing:0.5px;border-bottom:1px solid #e5e7eb;font-family:Arial,sans-serif;">Alan</th>
                    <th style="padding:10px 14px;text-align:left;font-size:11px;color:#6b7280;font-weight:700;text-transform:uppercase;letter-spacing:0.5px;border-bottom:1px solid #e5e7eb;font-family:Arial,sans-serif;">Eski Değer</th>
                    <th style="padding:10px 14px;text-align:left;font-size:11px;color:#6b7280;font-weight:700;text-transform:uppercase;letter-spacing:0.5px;border-bottom:1px solid #e5e7eb;font-family:Arial,sans-serif;">Yeni Değer</th>
                  </tr>
                  {changeRows}
                </table>
              </td>
            </tr>

            {ctaRow}

            <!-- Contact -->
            <tr>
              <td style="padding:0 32px 28px;">
                <p style="margin:0;color:#6b7280;font-size:13px;line-height:1.6;font-family:Arial,sans-serif;">
                  Sorularınız için müşteri hizmetlerimizle iletişime geçebilirsiniz.<br>
                  <strong style="color:#0a1628;">Acil Durum Hattı: 0532 015 26 38</strong>
                </p>
              </td>
            </tr>

            {BuildEmailFooter()}
            """;

        return WrapInLayout(bodyRows);
    }
}
