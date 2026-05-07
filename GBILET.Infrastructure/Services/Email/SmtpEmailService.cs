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

    public async Task SendSupportReplyNotificationAsync(
        string toEmail,
        string toName,
        string ticketNumber,
        string subject,
        string replyBody,
        CancellationToken ct = default)
    {
        var html = BuildReplyTemplate(toName, ticketNumber, subject, replyBody);
        await SendAsync(toEmail, toName, $"Destek Talebinize Yanıt Geldi — {ticketNumber}", html, ct);
    }

    public async Task SendTicketClosedNotificationAsync(
        string toEmail,
        string toName,
        string ticketNumber,
        string subject,
        CancellationToken ct = default)
    {
        var html = BuildClosedTemplate(toName, ticketNumber, subject);
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

            using var client = new SmtpClient();
            await client.ConnectAsync(_opts.Host, _opts.Port, SecureSocketOptions.SslOnConnect, ct);
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

    private static string BuildReplyTemplate(string name, string ticketNumber, string subject, string replyBody) => $"""
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
                    <h1 style="margin:0;color:#ffffff;font-size:22px;font-weight:700;letter-spacing:1px;">ATA<span style="color:#047857;">BİLET</span></h1>
                    <p style="margin:4px 0 0;color:#94a3b8;font-size:13px;">atabilet.com — Destek Merkezi</p>
                  </td>
                </tr>
                <!-- Body -->
                <tr>
                  <td style="padding:32px;">
                    <p style="margin:0 0 16px;color:#374151;font-size:15px;">Merhaba <strong>{name}</strong>,</p>
                    <p style="margin:0 0 24px;color:#374151;font-size:15px;">
                      <strong>{ticketNumber}</strong> numaralı "<em>{subject}</em>" başlıklı destek talebinize yanıt geldi.
                    </p>
                    <!-- Reply box -->
                    <div style="background:#f0fdf4;border-left:4px solid #047857;border-radius:4px;padding:20px;margin-bottom:24px;">
                      <p style="margin:0 0 8px;color:#047857;font-size:12px;font-weight:700;text-transform:uppercase;letter-spacing:1px;">Destek Ekibinin Yanıtı</p>
                      <p style="margin:0;color:#374151;font-size:14px;line-height:1.7;white-space:pre-wrap;">{System.Net.WebUtility.HtmlEncode(replyBody)}</p>
                    </div>
                    <p style="margin:0 0 24px;color:#6b7280;font-size:14px;">
                      Talebinizin tamamını görüntülemek ve yanıt vermek için aşağıdaki butona tıklayın.
                    </p>
                    <a href="https://atabilet.com/destek-taleplerim" style="display:inline-block;background:#047857;color:#ffffff;text-decoration:none;padding:12px 28px;border-radius:6px;font-size:14px;font-weight:600;">
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

    private static string BuildClosedTemplate(string name, string ticketNumber, string subject) => $"""
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
                    <h1 style="margin:0;color:#ffffff;font-size:22px;font-weight:700;letter-spacing:1px;">ATA<span style="color:#047857;">BİLET</span></h1>
                    <p style="margin:4px 0 0;color:#94a3b8;font-size:13px;">atabilet.com — Destek Merkezi</p>
                  </td>
                </tr>
                <!-- Body -->
                <tr>
                  <td style="padding:32px;">
                    <p style="margin:0 0 16px;color:#374151;font-size:15px;">Merhaba <strong>{name}</strong>,</p>
                    <p style="margin:0 0 24px;color:#374151;font-size:15px;">
                      <strong>{ticketNumber}</strong> numaralı "<em>{subject}</em>" başlıklı destek talebiniz çözüme kavuşturularak kapatılmıştır.
                    </p>
                    <div style="background:#f0f9ff;border-left:4px solid #0a1628;border-radius:4px;padding:20px;margin-bottom:24px;">
                      <p style="margin:0;color:#374151;font-size:14px;line-height:1.7;">
                        Talebinizle ilgili başka sorunuz varsa yeni bir destek talebi oluşturabilirsiniz.
                      </p>
                    </div>
                    <a href="https://atabilet.com/destek-taleplerim" style="display:inline-block;background:#047857;color:#ffffff;text-decoration:none;padding:12px 28px;border-radius:6px;font-size:14px;font-weight:600;">
                      Taleplerim
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
