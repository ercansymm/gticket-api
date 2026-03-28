namespace GBILET.Core.Models.Flight;


public class MakePaymentRequest
{
    /// <summary>
    /// MakePreBooking adimindan alinan SessionId.
    /// </summary>
    public string SessionId { get; set; } = null!;

    /// <summary>
    /// MakePreBooking adimindan alinan SessionToken.
    /// </summary>
    public string SessionToken { get; set; } = null!;

    /// <summary>
    /// MakePreBooking/Allocate adimindan alinan ShoppingFileId.
    /// </summary>
    public string ShoppingFileId { get; set; } = null!;

    /// <summary>
    /// Odeme yapilacak ProductId (AirBookings[0].ProductId).
    /// </summary>
    public string ProductId { get; set; } = null!;

    /// <summary>
    /// Odeme tutari.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Para birimi (varsayilan: TRY).
    /// </summary>
    public string Currency { get; set; } = "TRY";

    /// <summary>
    /// Odeme tipi:
    ///   "CreditCard"        → 3D Secure kredi karti odemesi (MakePayment_Init3DPayment)
    ///   "CreditCardDirect"  → 3D'siz dogrudan kredi karti odemesi (MakePayment_FromCreditCard)
    ///   "RunningAccount"    → Cari hesap odemesi (MakePayment_FromRunningAccount)
    /// </summary>
    public string PaymentType { get; set; } = "CreditCard";

    /// <summary>
    /// Kredi karti bilgileri (PaymentType = "CreditCard" veya "CreditCardDirect" ise zorunlu).
    /// </summary>
    public CreditCardInfo? CreditCard { get; set; }

    /// <summary>
    /// Taksitli odeme icin secilen taksit secenegi ID'si.
    /// MakePayment response'undaki InstallmentOptions listesinden secilir.
    /// Bos birakilirsa tek cekim (pesin) olarak islem yapilir.
    /// </summary>
    public string? InstallmentOptionId { get; set; }

    /// <summary>
    /// Kismi odeme mi? Varsayilan: false (tam odeme).
    /// </summary>
    public bool IsPartialPayment { get; set; } = false;

    /// <summary>
    /// Son satici komisyonunu dus? Varsayilan: false.
    /// </summary>
    public bool DeductLastSellerCommission { get; set; } = false;

    /// <summary>
    /// DB'deki booking ID'si (odeme kaydini eslestirir).
    /// </summary>
    public Guid? BookingId { get; set; }

    /// <summary>
    /// 3D Secure callback URL'i. Frontend tarafindan verilmezse
    /// sunucu kendi base URL'ini kullanir.
    /// </summary>
    public string? ContinueUrl { get; set; }
}

public class CreditCardInfo
{
    /// <summary>
    /// Kart sahibinin adi soyadi.
    /// </summary>
    public string CardHolderName { get; set; } = null!;

    /// <summary>
    /// Kart numarasi (16 hane, bosluksuz).
    /// </summary>
    public string CardNumber { get; set; } = null!;

    /// <summary>
    /// Son kullanma ayi (MM). Ornek: "01", "12"
    /// </summary>
    public string ExpiryMonth { get; set; } = null!;

    /// <summary>
    /// Son kullanma yili (YYYY). Ornek: "2026"
    /// </summary>
    public string ExpiryYear { get; set; } = null!;

    /// <summary>
    /// CVV / CVC (3 veya 4 hane).
    /// </summary>
    public string Cvv { get; set; } = null!;
}
