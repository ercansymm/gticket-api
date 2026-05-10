using GBILET.Core.DTOs.Support;

namespace GBILET.Core.Service.Email;

public record BookingEmailFlight(
    string From,
    string To,
    string FlightNumber,
    string Airline,
    DateTime DepartureDate,
    string? DepartureTime,
    string? ArrivalTime,
    string? Baggage);

public record BookingEmailPassenger(
    string FullName,
    string Type,
    string? TicketNumber,
    string? CitizenNo = null,
    string? Phone = null);

public interface IEmailService
{
    Task SendTicketCreatedNotificationAsync(
        string toEmail,
        string toName,
        string ticketNumber,
        string subject,
        Guid ticketId,
        CancellationToken ct = default);

    Task SendSupportReplyNotificationAsync(
        string toEmail,
        string toName,
        string ticketNumber,
        string subject,
        string replyBody,
        Guid ticketId,
        IReadOnlyList<SupportTicketMessageDto> allMessages,
        CancellationToken ct = default);

    Task SendTicketClosedNotificationAsync(
        string toEmail,
        string toName,
        string ticketNumber,
        string subject,
        Guid ticketId,
        CancellationToken ct = default);

    Task SendBookingConfirmationAsync(
        string toEmail,
        string toName,
        string pnr,
        Guid bookingId,
        IReadOnlyList<BookingEmailFlight> flights,
        IReadOnlyList<BookingEmailPassenger> passengers,
        decimal? grandTotal,
        string? currency,
        string pdfDownloadUrl,
        CancellationToken ct = default);
}
