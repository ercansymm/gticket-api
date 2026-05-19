namespace GBILET.Core.Models.Flight;

public class FinalizeShoppingRequest
{
    /// <summary>
    /// MakePayment adimindan alinan SessionId.
    /// </summary>
    public string SessionId { get; set; } = null!;

    /// <summary>
    /// MakePayment adimindan alinan SessionToken.
    /// </summary>
    public string SessionToken { get; set; } = null!;

    /// <summary>
    /// Shopping dosya ID'si.
    /// </summary>
    public string ShoppingFileId { get; set; } = null!;

    /// <summary>
    /// Biletlenecek ProductId (AirBookings[0].ProductId).
    /// </summary>
    public string ProductId { get; set; } = null!;

    /// <summary>
    /// DB'deki booking ID'si (durum guncelleme icin).
    /// </summary>
    public Guid? BookingId { get; set; }

    /// <summary>
    /// Fatura bilgileri (FinalizeShopping icin gerekli).
    /// Null gonderilirse varsayilan degerler kullanilir.
    /// </summary>
    public ShoppingBillingInfo? BillingInfo { get; set; }
}
