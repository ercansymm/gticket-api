namespace GBILET.Core.Models.Flight;

/// <summary>
/// Odeme alinmis ama biletleme yapilamamis booking'leri kurtarmak icin kullanilir.
/// </summary>
public class RecoverBookingRequest
{
    /// <summary>
    /// Kurtarilacak booking ID'si.
    /// </summary>
    public Guid BookingId { get; set; }

    /// <summary>
    /// BiletBank session ID'si (opsiyonel — yoksa DB'den alinir).
    /// </summary>
    public string? SessionId { get; set; }

    /// <summary>
    /// BiletBank session token (opsiyonel — yoksa DB'den alinir).
    /// </summary>
    public string? SessionToken { get; set; }

    /// <summary>
    /// Shopping file ID'si (opsiyonel — yoksa DB'den alinir).
    /// </summary>
    public string? ShoppingFileId { get; set; }

    /// <summary>
    /// Product ID'si (opsiyonel — yoksa cache'ten alinir).
    /// </summary>
    public string? ProductId { get; set; }

    /// <summary>
    /// Fatura bilgileri (opsiyonel).
    /// </summary>
    public ShoppingBillingInfo? BillingInfo { get; set; }
}
