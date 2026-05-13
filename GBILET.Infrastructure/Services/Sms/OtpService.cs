using GBILET.Core.Entities;
using GBILET.Core.Enums;
using GBILET.Core.Service.Sms;
using GBILET.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GBILET.Infrastructure.Services.Sms;

public class OtpService : IOtpService
{
    private const int OtpTtlMinutes = 5;
    private const int MaxAttempts = 3;

    private readonly GTicketDbContext _db;

    public OtpService(GTicketDbContext db) => _db = db;

    public async Task<string> GenerateAndStoreAsync(string phone, OtpPurpose purpose, CancellationToken ct = default)
    {
        // Aynı telefon + purpose için bekleyen eski OTP'leri geçersiz kıl
        var existing = await _db.OtpCodes
            .Where(o => o.Phone == phone && o.Purpose == (int)purpose && !o.IsUsed && o.ExpiresAt > DateTime.UtcNow)
            .ToListAsync(ct);

        foreach (var old in existing)
            old.IsUsed = true;

        var code = GenerateCode();

        _db.OtpCodes.Add(new OtpCode
        {
            Phone     = phone,
            Code      = code,
            Purpose   = (int)purpose,
            ExpiresAt = DateTime.UtcNow.AddMinutes(OtpTtlMinutes),
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);
        return code;
    }

    public async Task<OtpValidationResult> ValidateAsync(string phone, string code, OtpPurpose purpose, CancellationToken ct = default)
    {
        var otp = await _db.OtpCodes
            .Where(o => o.Phone == phone && o.Purpose == (int)purpose && !o.IsUsed)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (otp is null)
            return new OtpValidationResult(OtpValidationStatus.NotFound);

        if (otp.ExpiresAt < DateTime.UtcNow)
            return new OtpValidationResult(OtpValidationStatus.Expired);

        if (otp.AttemptCount >= MaxAttempts)
            return new OtpValidationResult(OtpValidationStatus.MaxAttemptsReached);

        if (otp.Code != code)
        {
            otp.AttemptCount++;
            await _db.SaveChangesAsync(ct);
            var remaining = MaxAttempts - otp.AttemptCount;
            return new OtpValidationResult(OtpValidationStatus.InvalidCode, remaining);
        }

        otp.IsUsed = true;
        await _db.SaveChangesAsync(ct);
        return new OtpValidationResult(OtpValidationStatus.Success);
    }

    private static string GenerateCode() =>
        Random.Shared.Next(100_000, 999_999).ToString();
}
