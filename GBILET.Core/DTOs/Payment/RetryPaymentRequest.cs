using GBILET.Core.Models.Flight;

namespace GBILET.Core.DTOs.Payment;

/// <summary>
/// Var olan bir rezervasyon icin yeniden odeme deneme istegi.
/// 3D fail veya iptal sonrasinda kullanilir — yeni allocate/prebooking yapilmaz,
/// rezervasyonda saklanan SessionId/SessionToken/ShoppingFileId/ProductId
/// tekrar kullanilir, sadece kart bilgisi yenilenir.
/// </summary>
public class RetryPaymentRequest
{
    /// <summary>
    /// Yeniden odeme yapilacak Booking.Id.
    /// </summary>
    public Guid BookingId { get; set; }

    /// <summary>
    /// Yeni kart bilgileri.
    /// </summary>
    public CreditCardInfo CreditCard { get; set; } = null!;

    /// <summary>
    /// Taksit secenegi (varsa).
    /// </summary>
    public string? InstallmentOptionId { get; set; }

    /// <summary>
    /// Fatura bilgisi (opsiyonel — ilk denemedekiyle ayni olmali).
    /// </summary>
    public ShoppingBillingInfo? BillingInfo { get; set; }
}
