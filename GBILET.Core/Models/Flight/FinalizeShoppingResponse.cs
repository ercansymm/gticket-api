namespace GBILET.Core.Models.Flight;

public class FinalizeShoppingResponse
{
    public bool HasError { get; set; }
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Biletleme basarili mi? (Status = Booking/Ticketed/Reservation ise true)
    /// </summary>
    public bool IsFinalized { get; set; }

    /// <summary>
    /// PNR kodu
    /// </summary>
    public string? BookingCode { get; set; }

    /// <summary>
    /// Biletleme durumu (Ticketed, Confirmed, Failed vb.)
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// E-bilet numaralari (yolcu bazli)
    /// </summary>
    public List<TicketInfo> Tickets { get; set; } = [];

    /// <summary>
    /// Shopping dosya ID'si
    /// </summary>
    public string? ShoppingFileId { get; set; }

    /// <summary>
    /// Para birimi
    /// </summary>
    public string? Currency { get; set; }

    /// <summary>
    /// Toplam tutar
    /// </summary>
    public decimal TotalFare { get; set; }

    /// <summary>
    /// Debug: Gonderilen SOAP request
    /// </summary>
    public string? RawSoapRequest { get; set; }

    /// <summary>
    /// Debug: Alinan SOAP response
    /// </summary>
    public string? RawSoapResponse { get; set; }
}

public class TicketInfo
{
    /// <summary>
    /// Yolcu adi
    /// </summary>
    public string? FirstName { get; set; }

    /// <summary>
    /// Yolcu soyadi
    /// </summary>
    public string? LastName { get; set; }

    /// <summary>
    /// Yolcu tipi (ADT, CHD, INF)
    /// </summary>
    public string? PaxType { get; set; }

    /// <summary>
    /// E-bilet numarasi
    /// </summary>
    public string? TicketNumber { get; set; }

    /// <summary>
    /// Yolcu sira numarasi
    /// </summary>
    public int SequenceNo { get; set; }
}
