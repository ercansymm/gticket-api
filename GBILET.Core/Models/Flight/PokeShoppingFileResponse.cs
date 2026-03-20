namespace GBILET.Core.Models.Flight;

public class PokeShoppingFileResponse
{
    public bool HasError { get; set; }
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Shopping dosya ID'si
    /// </summary>
    public string? ShoppingFileId { get; set; }

    /// <summary>
    /// Guncel dosya durumu
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// Fiyat degisti mi?
    /// </summary>
    public bool IsPriceChanged { get; set; }

    /// <summary>
    /// Ucus bilgisi degisti mi?
    /// </summary>
    public bool IsFlightInfoChanged { get; set; }

    /// <summary>
    /// Kalan odeme tutari
    /// </summary>
    public decimal RemainingSum { get; set; }

    /// <summary>
    /// Para birimi
    /// </summary>
    public string? Currency { get; set; }

    /// <summary>
    /// Rezervasyon iptal edildi mi?
    /// </summary>
    public bool IsReservationCancelled { get; set; }

    /// <summary>
    /// Booking kodu (PNR)
    /// </summary>
    public string? BookingCode { get; set; }

    /// <summary>
    /// Toplam tutar
    /// </summary>
    public decimal GrandTotal { get; set; }
}
