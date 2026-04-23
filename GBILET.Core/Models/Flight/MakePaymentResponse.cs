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
    /// Odeme sonrasi durum (T_AirBooking.Status: Reservation, Ticketed vb.)
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// Shopping dosya ID'si
    /// </summary>
    public string? ShoppingFileId { get; set; }

    /// <summary>
    /// Kalan odeme tutari
    /// </summary>
    public decimal RemainingSum { get; set; }

    /// <summary>
    /// Para birimi
    /// </summary>
    public string? Currency { get; set; }

    /// <summary>
    /// Odeme referans numarasi (PaymentId)
    /// </summary>
    public string? PaymentReferenceId { get; set; }

    /// <summary>
    /// Rezervasyon kodu (PNR) � BookingCode
    /// </summary>
    public string? PNR { get; set; }

    /// <summary>
    /// Booking durumu (T_AirBooking altindan: Reservation, Ticketed vb.)
    /// </summary>
    public string? BookingStatus { get; set; }


    /// <summary>
    /// Odeme toplam tutari
    /// </summary>
    public decimal GrandTotal { get; set; }

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

    /// <summary>
    /// Taksit secenekleri (kredi karti odemeleri icin).
    /// BiletBank PaymentInstallmentOptions olarak doner.
    /// </summary>
    public List<PaymentInstallmentOption> InstallmentOptions { get; set; } = [];

    /// <summary>
    /// Backend auto-finalize yapildiysa true doner.
    /// Frontend'in ayrica FinalizeShopping cagirmasina gerek kalmaz.
    /// </summary>
    public bool AutoFinalized { get; set; }

    /// <summary>
    /// Auto-finalize basarili olduysa biletleme durumu.
    /// </summary>
    public string? FinalizeStatus { get; set; }

    /// <summary>
    /// Auto-finalize basarili olduysa internal PNR.
    /// </summary>
    public string? InternalPnr { get; set; }

    /// <summary>
    /// Auto-finalize basarili olduysa bilet numaralari.
    /// </summary>
    public List<TicketInfo> Tickets { get; set; } = [];

    /// <summary>
    /// Debug: Gonderilen SOAP request
    /// </summary>
    public string? RawSoapRequest { get; set; }

    /// <summary>
    /// Debug: Alinan SOAP response
    /// </summary>
    public string? RawSoapResponse { get; set; }
}

/// <summary>
/// BiletBank taksit secenegi
/// </summary>
public class PaymentInstallmentOption
{
    public string? InstallmentOptionId { get; set; }
    public string? BankName { get; set; }
    public string? Program { get; set; }
    public int InstallmentCount { get; set; }
    public int TotalInstallmentCount { get; set; }
    public int BonusInstallmentCount { get; set; }
    public decimal MonthlyPayment { get; set; }
    public decimal SubTotal { get; set; }
    public decimal AmountOfInterest { get; set; }
    public decimal RateOfInterest { get; set; }
    public string? Currency { get; set; }
}
