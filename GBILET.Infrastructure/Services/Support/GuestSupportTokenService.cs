using GBILET.Core.Service.Support;
using Microsoft.AspNetCore.DataProtection;

namespace GBILET.Infrastructure.Services.Support;

public class GuestSupportTokenService : IGuestSupportTokenService
{
    private readonly IDataProtector _protector;

    public GuestSupportTokenService(IDataProtectionProvider provider)
    {
        // Sürüm purpose string'i: gelecekte format değişirse "v2" yapılır, eski tokenlar invalid olur.
        _protector = provider.CreateProtector("GBILET.GuestSupport.v1");
    }

    public (string Token, DateTime ExpiresAt) Issue(Guid bookingId, TimeSpan ttl)
    {
        var expiresAt = DateTime.UtcNow.Add(ttl);
        var exp = new DateTimeOffset(expiresAt, TimeSpan.Zero).ToUnixTimeSeconds();
        var payload = $"{bookingId:N}|{exp}";
        var token = _protector.Protect(payload);
        return (token, expiresAt);
    }

    public Guid? Validate(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        try
        {
            var raw = _protector.Unprotect(token);
            var parts = raw.Split('|');
            if (parts.Length != 2) return null;

            if (!Guid.TryParseExact(parts[0], "N", out var bookingId)) return null;
            if (!long.TryParse(parts[1], out var exp)) return null;

            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > exp)
                return null; // Süresi dolmuş

            return bookingId;
        }
        catch
        {
            // Geçersiz / tampered / yanlış purpose
            return null;
        }
    }
}
