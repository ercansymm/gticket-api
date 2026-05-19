namespace GBILET.Core.Service.Support;

/// <summary>
/// Misafir destek erişimi için kısa ömürlü, BookingId'ye kapsamlı access token üretir.
/// Token DataProtection ile şifrelenir; başka bir BookingId'ye erişim için kullanılamaz.
/// </summary>
public interface IGuestSupportTokenService
{
    /// <summary>
    /// Belirli bir BookingId için TTL süresi kadar geçerli token üretir.
    /// </summary>
    (string Token, DateTime ExpiresAt) Issue(Guid bookingId, TimeSpan ttl);

    /// <summary>
    /// Token'ı doğrular; geçerli ise içindeki BookingId'yi döner. Aksi halde null.
    /// </summary>
    Guid? Validate(string token);
}
