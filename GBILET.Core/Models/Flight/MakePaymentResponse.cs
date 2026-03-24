namespace GBILET.Core.Models.Flight;

public class MakePaymentResponse
{
    public bool HasError { get; set; }
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Odeme basarili mi?
    /// </summary>
    public bool IsPaymentSuccessful { get; set; }

    /// <summary>
    /// Odeme sonrasi durum
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// Shopping dosya ID'si
    /// </summary>
    public string? ShoppingFileId { get; set; }

    /// <summary>
    /// Kalan odeme tutari (kismi odeme icin)
    /// </summary>
    public decimal RemainingSum { get; set; }

    /// <summary>
    /// Para birimi
    /// </summary>
    public string? Currency { get; set; }

    /// <summary>
    /// Odeme referans numarasi
    /// </summary>
    public string? PaymentReferenceId { get; set; }

    /// <summary>
    /// 3D Secure yonlendirme URL'i (gerekiyorsa)
    /// </summary>
    public string? ThreeDSecureUrl { get; set; }

    /// <summary>
    /// 3D Secure gerekli mi?
    /// </summary>
    public bool Is3DSecureRequired { get; set; }

    /// <summary>
    /// 3D Secure HTML icerik (banka 3D dogrulama formu).
    /// Bu HTML'i kullaniciya iframe veya yeni pencerede gosterin.
    /// </summary>
    public string? ThreeDSecureHtml { get; set; }
}
