using GBILET.Core.Enums;

namespace GBILET.Core.Service.Sms;

public enum OtpValidationStatus { Success, InvalidCode, Expired, MaxAttemptsReached, NotFound }

public record OtpValidationResult(OtpValidationStatus Status, int RemainingAttempts = 0);

public interface IOtpService
{
    Task<string> GenerateAndStoreAsync(string phone, OtpPurpose purpose, CancellationToken ct = default);
    Task<OtpValidationResult> ValidateAsync(string phone, string code, OtpPurpose purpose, CancellationToken ct = default);
}
