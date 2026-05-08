using GBILET.Core.DTOs.Support;
using GBILET.Core.Entities.Support;
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
            // Hosting sağlayıcıları çoğunlukla farklı CN'li sertifika kullandığından her zaman bypass et
            client.ServerCertificateValidationCallback = (_, _, _, _) => true;

            await client.ConnectAsync(_opts.Host, _opts.Port, sslOption, ct);
            await client.AuthenticateAsync(_opts.Username, _opts.Password, ct);
            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);

            _logger.LogInformation("Email sent to {ToEmail} — {Subject}", toEmail, subject);
        }
        catch (Exception ex)
        {
            // Mail hatası ana akışı bozmasın — sadece logla
            _logger.LogError(ex, "Email sending failed to {ToEmail} with subject {Subject}", toEmail, subject);
        }
    }

    private static string BuildReplyTemplate(
        string name,
        string ticketNumber,
        string subject,
        Guid ticketId,
        IReadOnlyList<SupportTicketMessageDto> allMessages)
    {
        var ticketUrl = $"https://atabilet.com/destek-taleplerim/{ticketId}";
        var conversationHtml = BuildConversationHtml(allMessages);

        return $"""
            <!DOCTYPE html>
            <html lang="tr">
            <head><meta charset="UTF-8"><meta name="viewport" content="width=device-width, initial-scale=1.0"></head>
            <body style="margin:0;padding:0;background:#f4f6f8;font-family:Arial,sans-serif;">
              <table width="100%" cellpadding="0" cellspacing="0" style="background:#f4f6f8;padding:32px 0;">
                <tr><td align="center">
                  <table width="600" cellpadding="0" cellspacing="0" style="background:#ffffff;border-radius:8px;overflow:hidden;max-width:600px;">
                    <!-- Header -->
                    <tr>
                      <td style="background:#0a1628;padding:28px 32px;">
                        <h1 style="margin:0;color:#ffffff;font-size:22px;font-weight:700;letter-spacing:1px;">ATA<span style="color:#db2525;">BİLET</span></h1>
                        <p style="margin:4px 0 0;color:#94a3b8;font-size:13px;">atabilet.com — Destek Merkezi</p>
                      </td>
                    </tr>
                    <!-- Body -->
                    <tr>
                      <td style="padding:32px;">
                        <p style="margin:0 0 16px;color:#374151;font-size:15px;">Merhaba <strong>{name}</strong>,</p>
                        <p style="margin:0 0 24px;color:#374151;font-size:15px;">
                          <strong>{ticketNumber}</strong> numaralı "<em>{System.Net.WebUtility.HtmlEncode(subject)}</em>" başlıklı destek talebinize yanıt geldi.
                        </p>
                        <!-- Conversation -->
                        {conversationHtml}
                        <p style="margin:24px 0 24px;color:#6b7280;font-size:14px;">
                          Talebi görüntülemek ve yanıt vermek için aşağıdaki butona tıklayın.
                        </p>
                        <a href="{ticketUrl}" style="display:inline-block;background:#db2525;color:#ffffff;text-decoration:none;padding:12px 28px;border-radius:6px;font-size:14px;font-weight:600;">
                          Talebi Görüntüle ve Yanıtla
                        </a>
                      </td>
                    </tr>
                    <!-- Footer -->
                    <tr>
                      <td style="background:#f8fafc;padding:20px 32px;border-top:1px solid #e5e7eb;">
                        <p style="margin:0;color:#9ca3af;font-size:12px;text-align:center;">
                          Bu e-posta otomatik olarak gönderilmiştir. Lütfen bu adrese doğrudan yanıt vermeyiniz.<br>
                          © {DateTime.UtcNow.Year} Atabilet.com — Tüm hakları saklıdır.
                        </p>
                      </td>
                    </tr>
                  </table>
                </td></tr>
              </table>
            </body>
            </html>
            """;
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
        var totalHtml = grandTotal.HasValue
            ? $"<tr><td style=\"background:#f8fafc;border-top:1px solid #e5e7eb;padding:16px 20px;\"><table width=\"100%\" cellpadding=\"0\" cellspacing=\"0\"><tr><td style=\"color:#374151;font-size:14px;font-weight:600;\">Toplam Ödenen Tutar</td><td align=\"right\" style=\"color:#0a1628;font-size:16px;font-weight:700;\">{grandTotal.Value.ToString("N2")} {currency ?? "TRY"}</td></tr></table></td></tr>"
            : string.Empty;
        var year = DateTime.UtcNow.Year;

        return $"""
            <!DOCTYPE html>
            <html lang="tr">
            <head><meta charset="UTF-8"><meta name="viewport" content="width=device-width, initial-scale=1.0"></head>
            <body style="margin:0;padding:0;background:#f4f6f8;font-family:Arial,sans-serif;">
              <table width="100%" cellpadding="0" cellspacing="0" style="background:#f4f6f8;padding:32px 0;">
                <tr><td align="center">
                  <table width="600" cellpadding="0" cellspacing="0" style="background:#ffffff;border-radius:8px;overflow:hidden;max-width:600px;">

                    <!-- Header -->
                    <tr>
                      <td style="background:#0a1628;padding:28px 32px;">
                        <h1 style="margin:0;color:#ffffff;font-size:22px;font-weight:700;letter-spacing:1px;">ATA<span style="color:#db2525;">BİLET</span></h1>
                        <p style="margin:4px 0 0;color:#94a3b8;font-size:13px;">atabilet.com — Uçuş Bileti</p>
                      </td>
                    </tr>

                    <!-- Success banner -->
                    <tr>
                      <td style="background:#047857;padding:18px 32px;">
                        <table width="100%" cellpadding="0" cellspacing="0">
                          <tr>
                            <td>
                              <p style="margin:0;color:#ffffff;font-size:18px;font-weight:700;">Biletiniz Başarıyla Oluşturuldu!</p>
                              <p style="margin:4px 0 0;color:#a7f3d0;font-size:13px;">Bilet bilgileriniz aşağıda yer almaktadır.</p>
                            </td>
                            <td align="right">
                              <div style="background:#065f46;border-radius:6px;padding:10px 16px;display:inline-block;">
                                <p style="margin:0;color:#6ee7b7;font-size:11px;font-weight:600;text-transform:uppercase;letter-spacing:0.5px;">Rezervasyon Kodu</p>
                                <p style="margin:2px 0 0;color:#ffffff;font-size:18px;font-weight:700;letter-spacing:2px;">{pnr}</p>
                              </div>
                            </td>
                          </tr>
                        </table>
                      </td>
                    </tr>

                    <!-- Body -->
                    <tr>
                      <td style="padding:28px 32px 8px;">
                        <p style="margin:0 0 20px;color:#374151;font-size:15px;">Merhaba <strong>{System.Net.WebUtility.HtmlEncode(name)}</strong>,</p>
                        <p style="margin:0 0 24px;color:#6b7280;font-size:14px;line-height:1.6;">
                          Rezervasyon işleminiz tamamlanmış ve biletiniz oluşturulmuştur. Aşağıda uçuş ve yolcu bilgilerinizi bulabilirsiniz.
                        </p>

                        <!-- Uçuş Bilgileri -->
                        <p style="margin:0 0 10px;color:#0a1628;font-size:13px;font-weight:700;text-transform:uppercase;letter-spacing:0.5px;">Uçuş Bilgileri</p>
                        <table width="100%" cellpadding="0" cellspacing="0" style="border:1px solid #e5e7eb;border-radius:6px;overflow:hidden;margin-bottom:24px;">
                          {flightsHtml}
                        </table>

                        <!-- Yolcu Bilgileri -->
                        <p style="margin:0 0 10px;color:#0a1628;font-size:13px;font-weight:700;text-transform:uppercase;letter-spacing:0.5px;">Yolcu Bilgileri</p>
                        <table width="100%" cellpadding="0" cellspacing="0" style="border:1px solid #e5e7eb;border-radius:6px;overflow:hidden;margin-bottom:24px;">
                          <tr style="background:#f8fafc;">
                            <td style="padding:10px 16px;color:#6b7280;font-size:12px;font-weight:700;text-transform:uppercase;border-bottom:1px solid #e5e7eb;">Yolcu</td>
                            <td style="padding:10px 16px;color:#6b7280;font-size:12px;font-weight:700;text-transform:uppercase;border-bottom:1px solid #e5e7eb;">Tip</td>
                            <td style="padding:10px 16px;color:#6b7280;font-size:12px;font-weight:700;text-transform:uppercase;border-bottom:1px solid #e5e7eb;">Bilet No</td>
                          </tr>
                          {passengersHtml}
                          {totalHtml}
                        </table>

                        <!-- CTA -->
                        <table width="100%" cellpadding="0" cellspacing="0" style="margin-bottom:8px;">
                          <tr>
                            <td>
                              <a href="{pdfDownloadUrl}" style="display:inline-block;background:#db2525;color:#ffffff;text-decoration:none;padding:14px 32px;border-radius:6px;font-size:15px;font-weight:700;">
                                E-Bilet İndir (PDF)
                              </a>
                            </td>
                          </tr>
                        </table>
                        <p style="margin:12px 0 20px;color:#9ca3af;font-size:12px;">
                          E-biletinizi indirerek uçuşta ibraz edebilirsiniz.
                        </p>
                      </td>
                    </tr>

                    <!-- Info box -->
                    <tr>
                      <td style="padding:0 32px 28px;">
                        <div style="background:#fffbeb;border:1px solid #fcd34d;border-radius:6px;padding:14px 16px;">
                          <p style="margin:0 0 4px;color:#92400e;font-size:13px;font-weight:700;">Önemli Hatırlatma</p>
                          <p style="margin:0;color:#92400e;font-size:13px;line-height:1.5;">
                            Uçuştan en az 2 saat önce havalimanında olunuz. Kimlik/pasaport belgenizi yanınızda bulundurunuz.
                          </p>
                        </div>
                      </td>
                    </tr>

                    <!-- Footer -->
                    <tr>
                      <td style="background:#f8fafc;padding:20px 32px;border-top:1px solid #e5e7eb;">
                        <p style="margin:0;color:#9ca3af;font-size:12px;text-align:center;">
                          Bu e-posta otomatik olarak gönderilmiştir. Sorularınız için destek ekibimize ulaşabilirsiniz.<br>
                          © {year} Atabilet.com — Tüm hakları saklıdır.
                        </p>
                      </td>
                    </tr>

                  </table>
                </td></tr>
              </table>
            </body>
            </html>
            """;
    }

    private static string BuildFlightsHtml(IReadOnlyList<BookingEmailFlight> flights)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < flights.Count; i++)
        {
            var f = flights[i];
            var border = i < flights.Count - 1 ? "border-bottom:1px solid #e5e7eb;" : string.Empty;
            var dateStr = f.DepartureDate.ToString("dd MMMM yyyy", new System.Globalization.CultureInfo("tr-TR"));
            var timeStr = !string.IsNullOrEmpty(f.DepartureTime) && !string.IsNullOrEmpty(f.ArrivalTime)
                ? $"{f.DepartureTime} → {f.ArrivalTime}"
                : f.DepartureTime ?? string.Empty;
            var baggage = !string.IsNullOrEmpty(f.Baggage) ? $"<span style=\"color:#6b7280;font-size:12px;\"> · Bagaj: {System.Net.WebUtility.HtmlEncode(f.Baggage)}</span>" : string.Empty;

            sb.Append(
                $"<tr><td style=\"padding:16px 20px;{border}\">" +
                $"<table width=\"100%\" cellpadding=\"0\" cellspacing=\"0\"><tr>" +
                $"<td style=\"width:40%;\">" +
                $"<p style=\"margin:0;color:#0a1628;font-size:22px;font-weight:700;\">{System.Net.WebUtility.HtmlEncode(f.From)}</p>" +
                $"<p style=\"margin:2px 0 0;color:#6b7280;font-size:12px;\">{dateStr}</p>" +
                $"</td>" +
                $"<td align=\"center\" style=\"width:20%;color:#94a3b8;font-size:18px;font-weight:300;\">→</td>" +
                $"<td style=\"width:40%;text-align:right;\">" +
                $"<p style=\"margin:0;color:#0a1628;font-size:22px;font-weight:700;\">{System.Net.WebUtility.HtmlEncode(f.To)}</p>" +
                $"<p style=\"margin:2px 0 0;color:#6b7280;font-size:12px;\">{System.Net.WebUtility.HtmlEncode(timeStr)}</p>" +
                $"</td>" +
                $"</tr></table>" +
                $"<p style=\"margin:8px 0 0;color:#374151;font-size:13px;\">" +
                $"{System.Net.WebUtility.HtmlEncode(f.Airline)} · {System.Net.WebUtility.HtmlEncode(f.FlightNumber)}{baggage}</p>" +
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
            var border = i < passengers.Count - 1 ? "border-bottom:1px solid #e5e7eb;" : string.Empty;
            var typeLabel = typeMap.TryGetValue(p.Type, out var label) ? label : p.Type;
            var ticketDisplay = !string.IsNullOrEmpty(p.TicketNumber)
                ? System.Net.WebUtility.HtmlEncode(p.TicketNumber)
                : "<span style=\"color:#9ca3af;\">—</span>";

            sb.Append(
                $"<tr>" +
                $"<td style=\"padding:12px 16px;color:#374151;font-size:14px;{border}\">{System.Net.WebUtility.HtmlEncode(p.FullName)}</td>" +
                $"<td style=\"padding:12px 16px;color:#6b7280;font-size:13px;{border}\">{typeLabel}</td>" +
                $"<td style=\"padding:12px 16px;color:#374151;font-size:13px;font-family:monospace;{border}\">{ticketDisplay}</td>" +
                $"</tr>"
            );
        }
        return sb.ToString();
    }

    private static string BuildConversationHtml(IReadOnlyList<SupportTicketMessageDto> messages)
    {
        if (messages.Count == 0) return string.Empty;

        var sb = new System.Text.StringBuilder();
        sb.Append("<p style=\"margin:0 0 12px;color:#6b7280;font-size:12px;font-weight:700;text-transform:uppercase;letter-spacing:0.5px;\">Konuşma Geçmişi</p>");

        foreach (var msg in messages)
        {
            var isAdmin = msg.SenderType == SupportMessageSenderType.Admin;
            var bg = isAdmin ? "#f0fdf4" : "#f8fafc";
            var border = isAdmin ? "#047857" : "#d1d5db";
            var labelColor = isAdmin ? "#047857" : "#374151";
            var label = isAdmin ? "Destek Ekibi" : System.Net.WebUtility.HtmlEncode(msg.SenderDisplayName);
            var time = msg.CreatedAt.ToString("dd.MM.yyyy HH:mm");
            var body = System.Net.WebUtility.HtmlEncode(msg.Body);

            sb.Append(
                $"<table width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"margin-bottom:10px;\">" +
                $"<tr><td style=\"background:{bg};border-left:4px solid {border};border-radius:4px;padding:14px 16px;\">" +
                $"<table width=\"100%\" cellpadding=\"0\" cellspacing=\"0\">" +
                $"<tr><td style=\"color:{labelColor};font-size:12px;font-weight:700;\">{label}</td>" +
                $"<td align=\"right\" style=\"color:#9ca3af;font-size:11px;\">{time}</td></tr></table>" +
                $"<p style=\"margin:8px 0 0;color:#374151;font-size:14px;line-height:1.7;white-space:pre-wrap;\">{body}</p>" +
                $"</td></tr></table>"
            );
        }

        return sb.ToString();
    }

    private static string BuildClosedTemplate(string name, string ticketNumber, string subject, Guid ticketId)
    {
        var ticketUrl = $"https://atabilet.com/destek-taleplerim/{ticketId}";

        return $"""
            <!DOCTYPE html>
            <html lang="tr">
            <head><meta charset="UTF-8"><meta name="viewport" content="width=device-width, initial-scale=1.0"></head>
            <body style="margin:0;padding:0;background:#f4f6f8;font-family:Arial,sans-serif;">
              <table width="100%" cellpadding="0" cellspacing="0" style="background:#f4f6f8;padding:32px 0;">
                <tr><td align="center">
                  <table width="600" cellpadding="0" cellspacing="0" style="background:#ffffff;border-radius:8px;overflow:hidden;max-width:600px;">
                    <!-- Header -->
                    <tr>
                      <td style="background:#0a1628;padding:28px 32px;">
                        <h1 style="margin:0;color:#ffffff;font-size:22px;font-weight:700;letter-spacing:1px;">ATA<span style="color:#db2525;">BİLET</span></h1>
                        <p style="margin:4px 0 0;color:#94a3b8;font-size:13px;">atabilet.com — Destek Merkezi</p>
                      </td>
                    </tr>
                    <!-- Body -->
                    <tr>
                      <td style="padding:32px;">
                        <p style="margin:0 0 16px;color:#374151;font-size:15px;">Merhaba <strong>{name}</strong>,</p>
                        <p style="margin:0 0 24px;color:#374151;font-size:15px;">
                          <strong>{ticketNumber}</strong> numaralı "<em>{System.Net.WebUtility.HtmlEncode(subject)}</em>" başlıklı destek talebiniz çözüme kavuşturularak kapatılmıştır.
                        </p>
                        <div style="background:#f0f9ff;border-left:4px solid #0a1628;border-radius:4px;padding:20px;margin-bottom:24px;">
                          <p style="margin:0;color:#374151;font-size:14px;line-height:1.7;">
                            Talebinizle ilgili başka sorunuz varsa yeni bir destek talebi oluşturabilirsiniz.
                          </p>
                        </div>
                        <a href="{ticketUrl}" style="display:inline-block;background:#db2525;color:#ffffff;text-decoration:none;padding:12px 28px;border-radius:6px;font-size:14px;font-weight:600;">
                          Talebi Görüntüle
                        </a>
                      </td>
                    </tr>
                    <!-- Footer -->
                    <tr>
                      <td style="background:#f8fafc;padding:20px 32px;border-top:1px solid #e5e7eb;">
                        <p style="margin:0;color:#9ca3af;font-size:12px;text-align:center;">
                          Bu e-posta otomatik olarak gönderilmiştir. Lütfen bu adrese doğrudan yanıt vermeyiniz.<br>
                          © {DateTime.UtcNow.Year} Atabilet.com — Tüm hakları saklıdır.
                        </p>
                      </td>
                    </tr>
                  </table>
                </td></tr>
              </table>
            </body>
            </html>
            """;
    }

    private static string BuildCreatedTemplate(string name, string ticketNumber, string subject, Guid ticketId)
    {
        var ticketUrl = $"https://atabilet.com/destek-taleplerim/{ticketId}";

        return $"""
            <!DOCTYPE html>
            <html lang="tr">
            <head><meta charset="UTF-8"><meta name="viewport" content="width=device-width, initial-scale=1.0"></head>
            <body style="margin:0;padding:0;background:#f4f6f8;font-family:Arial,sans-serif;">
              <table width="100%" cellpadding="0" cellspacing="0" style="background:#f4f6f8;padding:32px 0;">
                <tr><td align="center">
                  <table width="600" cellpadding="0" cellspacing="0" style="background:#ffffff;border-radius:8px;overflow:hidden;max-width:600px;">
                    <!-- Header -->
                    <tr>
                      <td style="background:#0a1628;padding:28px 32px;">
                        <h1 style="margin:0;color:#ffffff;font-size:22px;font-weight:700;letter-spacing:1px;">ATA<span style="color:#db2525;">BİLET</span></h1>
                        <p style="margin:4px 0 0;color:#94a3b8;font-size:13px;">atabilet.com — Destek Merkezi</p>
                      </td>
                    </tr>
                    <!-- Body -->
                    <tr>
                      <td style="padding:32px;">
                        <p style="margin:0 0 16px;color:#374151;font-size:15px;">Merhaba <strong>{name}</strong>,</p>
                        <p style="margin:0 0 24px;color:#374151;font-size:15px;">
                          Destek talebiniz başarıyla alınmıştır. Destek ekibimiz en kısa sürede sizinle iletişime geçecektir.
                        </p>
                        <div style="background:#f0fdf4;border-left:4px solid #047857;border-radius:4px;padding:20px;margin-bottom:24px;">
                          <p style="margin:0 0 8px;color:#047857;font-size:12px;font-weight:700;text-transform:uppercase;letter-spacing:1px;">Talep Bilgileri</p>
                          <p style="margin:0 0 4px;color:#374151;font-size:14px;"><strong>Talep No:</strong> {ticketNumber}</p>
                          <p style="margin:0;color:#374151;font-size:14px;"><strong>Konu:</strong> {System.Net.WebUtility.HtmlEncode(subject)}</p>
                        </div>
                        <a href="{ticketUrl}" style="display:inline-block;background:#db2525;color:#ffffff;text-decoration:none;padding:12px 28px;border-radius:6px;font-size:14px;font-weight:600;">
                          Talebi Görüntüle
                        </a>
                      </td>
                    </tr>
                    <!-- Footer -->
                    <tr>
                      <td style="background:#f8fafc;padding:20px 32px;border-top:1px solid #e5e7eb;">
                        <p style="margin:0;color:#9ca3af;font-size:12px;text-align:center;">
                          Bu e-posta otomatik olarak gönderilmiştir. Lütfen bu adrese doğrudan yanıt vermeyiniz.<br>
                          © {DateTime.UtcNow.Year} Atabilet.com — Tüm hakları saklıdır.
                        </p>
                      </td>
                    </tr>
                  </table>
                </td></tr>
              </table>
            </body>
            </html>
            """;
    }
}
