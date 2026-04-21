using GBILET.Core.Models.Flight;

namespace GBILET.Core.DTOs.Payment;

/// <summary>
/// PaymentService.ProcessPaymentAsync icin parametre.
/// Frontend'in gonderdigi MakePaymentRequest'i sarar ve servise iletir.
/// </summary>
public class PaymentProcessRequest
{
    /// <summary>
    /// Frontend'den gelen MakePayment istegi (BookingId, PaymentType, CreditCard, Amount vb.).
    /// </summary>
    public MakePaymentRequest Payment { get; set; } = null!;

    /// <summary>
    /// 3D Secure callback'i icin baz URL (controller HttpContext'ten uretir).
    /// Format: "https://host/api/Flight/3d-callback"
    /// Servis bu URL'in sonuna sid/stk/sfid/bid query parametrelerini ekler.
    /// </summary>
    public string? CallbackBaseUrl { get; set; }
}
