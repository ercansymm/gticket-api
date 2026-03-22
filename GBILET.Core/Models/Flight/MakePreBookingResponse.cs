namespace GBILET.Core.Models.Flight;

public class MakePreBookingResponse
{
    public bool HasError { get; set; }
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Shopping dosya ID'si
    /// </summary>
    public string? ShoppingFileId { get; set; }

    /// <summary>
    /// Fiyat degisti mi?
    /// </summary>
    public bool IsPriceChanged { get; set; }

    /// <summary>
    /// Ucus bilgisi degisti mi?
    /// </summary>
    public bool IsFlightInfoChanged { get; set; }

    /// <summary>
    /// Rezervasyon iptal edildi mi?
    /// </summary>
    public bool IsReservationCancelled { get; set; }

    /// <summary>
    /// Rezervasyon kodu (PNR)
    /// </summary>
    public string? BookingCode { get; set; }

    /// <summary>
    /// Urun ID
    /// </summary>
    public string? ProductId { get; set; }

    /// <summary>
    /// Booking durumu (Reserved, Confirmed, Failed vb.)
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// Taban ucret
    /// </summary>
    public decimal BaseFare { get; set; }

    /// <summary>
    /// Vergiler
    /// </summary>
    public decimal Taxes { get; set; }

    /// <summary>
    /// Servis ucreti
    /// </summary>
    public decimal ServiceFee { get; set; }

    /// <summary>
    /// Toplam ucret
    /// </summary>
    public decimal TotalFare { get; set; }

    /// <summary>
    /// Kalan odeme tutari
    /// </summary>
    public decimal RemainingSum { get; set; }

    /// <summary>
    /// Rezerve edilebilir mi?
    /// </summary>
    public bool CanBeReserved { get; set; }

    /// <summary>
    /// On rezervasyonun gecerlilik suresi
    /// </summary>
    public DateTime? PrebookingExpiresAt { get; set; }

    /// <summary>
    /// Rezervasyonun gecerlilik suresi
    /// </summary>
    public DateTime? ReservationExpiresAt { get; set; }

    /// <summary>
    /// Para birimi
    /// </summary>
    public string? Currency { get; set; }

    /// <summary>
    /// Yolcu bilgileri
    /// </summary>
    public List<PreBookingPassenger> Passengers { get; set; } = [];

    /// <summary>
    /// Segment bilgileri
    /// </summary>
    public List<PreBookingSegment> Segments { get; set; } = [];
}
