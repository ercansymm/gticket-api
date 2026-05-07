namespace GBILET.Core.Service.Email;

public interface IEmailService
{
    Task SendSupportReplyNotificationAsync(
        string toEmail,
        string toName,
        string ticketNumber,
        string subject,
        string replyBody,
        CancellationToken ct = default);

    Task SendTicketClosedNotificationAsync(
        string toEmail,
        string toName,
        string ticketNumber,
        string subject,
        CancellationToken ct = default);
}
