namespace GBILET.Core.Models.Flight;

public class BookingResponse
{
    public bool HasError { get; set; }
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Rezervasyon kodu (PNR)
    /// </summary>
    public string? PNR { get; set; }

    /// <summary>
    /// Booking durumu (Reserved, Confirmed, Failed vb.)
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// ShoppingFile ID
    /// </summary>
    public string? ShoppingFileId { get; set; }

    /// <summary>
    /// Urun ID
    /// </summary>
    public string? ProductId { get; set; }

    /// <summary>
    /// Toplam fiyat
    /// </summary>
    public decimal TotalFare { get; set; }

    /// <summary>
    /// Para birimi
    /// </summary>
    public string? Currency { get; set; }

    /// <summary>
    /// Yolcu bilgileri (BiletBank'tan donen)
    /// </summary>
    public List<BookingPassengerResult> Passengers { get; set; } = [];

    /// <summary>
    /// Segment bilgileri
    /// </summary>
    public List<BookingSegmentResult> Segments { get; set; } = [];

    /// <summary>
    /// Debug: Ham SOAP yaniti (gecici - production'da kaldirilacak)
    /// </summary>
    public string? RawSoapResponse { get; set; }

    /// <summary>
    /// Debug: Gonderilen SOAP request (gecici - production'da kaldirilacak)
    /// </summary>
    public string? RawSoapRequest { get; set; }

    /// <summary>
    /// UpdatePassengers adiminin SOAP request'i (log icin)
    /// </summary>
    public string? UpdatePassengersSoapRequest { get; set; }

    /// <summary>
    /// UpdatePassengers adiminin SOAP response'u (log icin)
    /// </summary>
    public string? UpdatePassengersSoapResponse { get; set; }
}

/// <summary>
/// Booking sonrasi donen yolcu bilgisi
/// </summary>
public class BookingPassengerResult
{
    public string? PaxType { get; set; }
    public int SequenceNo { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Gender { get; set; }
    public string? BirthDate { get; set; }

    /// <summary>
    /// Bilet numarasi (ticketing sonrasi dolar, booking asamasinda bos olabilir)
    /// </summary>
    public string? TicketNumber { get; set; }
}

/// <summary>
/// Booking sonrasi donen segment bilgisi
/// </summary>
public class BookingSegmentResult
{
    public int SequenceNo { get; set; }
    public string? OriginCode { get; set; }
    public string? DestinationCode { get; set; }
    public string? DepartureDay { get; set; }
    public string? DepartureTime { get; set; }
    public string? ArrivalDay { get; set; }
    public string? ArrivalTime { get; set; }
    public string? MarketingAirline { get; set; }
    public string? FlightNumber { get; set; }
    public string? BookingClass { get; set; }
    public string? Status { get; set; }
}
