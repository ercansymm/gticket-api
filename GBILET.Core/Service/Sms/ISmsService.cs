namespace GBILET.Core.Service.Sms;

public interface ISmsService
{
    Task<bool> SendOtpAsync(string phone, string code, string purposeLabel, CancellationToken ct = default);
    Task<bool> SendTicketConfirmationAsync(string phone, string passengerName, string pnr, string origin, string destination, DateTime departureTime, CancellationToken ct = default);
}
