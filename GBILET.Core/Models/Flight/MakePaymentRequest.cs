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
    /// Odeme tipi: "CreditCard" veya "RunningAccount".
    /// </summary>
    public string PaymentType { get; set; } = "CreditCard";

    /// <summary>
    /// Kredi karti bilgileri (PaymentType = CreditCard ise zorunlu).
    /// </summary>
    public CreditCardInfo? CreditCard { get; set; }

    /// <summary>
    /// DB'deki booking ID'si (odeme kaydini eslestirir).
    /// </summary>
    public Guid? BookingId { get; set; }
}

public class CreditCardInfo
{
    /// <summary>
    /// Kart sahibinin adi soyadi.
    /// </summary>
    public string CardHolderName { get; set; } = null!;

    /// <summary>
    /// Kart numarasi (16 hane).
    /// </summary>
    public string CardNumber { get; set; } = null!;

    /// <summary>
    /// Son kullanma ayi (MM).
    /// </summary>
    public string ExpiryMonth { get; set; } = null!;

    /// <summary>
    /// Son kullanma yili (YYYY).
    /// </summary>
    public string ExpiryYear { get; set; } = null!;

    /// <summary>
    /// CVV / CVC (3 veya 4 hane).
    /// </summary>
    public string Cvv { get; set; } = null!;
}
