using System.Security.Claims;
using GBILET.Core.Entities.Admin;

namespace GBILET.Core.Service.Admin;

public interface ITokenService
{
    /// <summary>
    /// Admin kullanıcısı için kısa ömürlü (15dk) access token üretir.
    /// </summary>
    (string Token, DateTime ExpiresAt) GenerateAccessToken(AdminUser user);

    /// <summary>
    /// Refresh token için random secure string üretir.
    /// DB'ye hash'lenmiş hali kaydedilir.
    /// </summary>
    string GenerateRefreshToken();

    /// <summary>
    /// Refresh token'ı SHA256 ile hashler (DB karşılaştırması için).
    /// </summary>
    string HashToken(string token);

    /// <summary>
    /// Access token'ı doğrular, expired olsa bile claim'leri döner.
    /// Refresh flow'unda kullanılır.
    /// </summary>
    ClaimsPrincipal? ValidateAccessTokenIgnoringExpiry(string token);
}