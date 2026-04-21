using GBILET.Core.DTOs.Payment;

namespace GBILET.Core.Interfaces;

/// <summary>
/// Odeme orkestrasyon servisi. BiletBank MakePayment / Complete3DPayment cagrilarini sarar
/// ve ayni transaction icinde Payment + Booking + BookingLog tablolarina yazar.
/// </summary>
public interface IPaymentService
{
    /// <summary>
    /// Yeni bir odeme baslatir (CreditCard / CreditCardDirect / RunningAccount).
    /// Basarili / basarisiz / 3D-bekliyor her durumda Payment tablosuna kayit yazar.
    /// </summary>
    Task<PaymentProcessResult> ProcessPaymentAsync(PaymentProcessRequest request, CancellationToken ct = default);

    /// <summary>
    /// 3D Secure dogrulamasi tamamlandiktan sonra Complete3DPayment cagrir,
    /// basariliysa Booking'i Paid'e ceker ve (mumkunse) auto-finalize ile bilet keser.
    /// </summary>
    Task<PaymentProcessResult> ProcessComplete3DAsync(Complete3DRequest request, CancellationToken ct = default);
}
