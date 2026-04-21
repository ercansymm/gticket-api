using GBILET.Core.Models.Flight;

namespace GBILET.Core.DTOs.Payment;

/// <summary>
/// PaymentService.ProcessComplete3DAsync icin parametre.
/// 3D dogrulamasi sonrasi BiletBank'a Complete3D gondermek + booking'i finalize etmek icin gerekli her sey.
/// </summary>
public class Complete3DRequest
{
    /// <summary>
    /// BiletBank Complete3DPayment cagrisi icin temel istek.
    /// </summary>
    public Complete3DPaymentRequest Payment { get; set; } = null!;

    /// <summary>
    /// Auto-finalize icin gereken ProductId (Allocate response'taki AirBookings[0].ProductId).
    /// Bos birakilirsa finalize denenmez, sadece odeme tamamlanir.
    /// </summary>
    public string? ProductId { get; set; }

    /// <summary>
    /// Auto-finalize icin gereken fatura bilgileri.
    /// </summary>
    public ShoppingBillingInfo? BillingInfo { get; set; }
}
