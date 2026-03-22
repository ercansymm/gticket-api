namespace GBILET.Core.Models.Flight;

public class ReadShoppingFileResponse
{
    public bool HasError { get; set; }
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Shopping dosya ID'si
    /// </summary>
    public string? ShoppingFileId { get; set; }

    /// <summary>
    /// Dosya durumu
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// PNR kodu
    /// </summary>
    public string? BookingCode { get; set; }

    /// <summary>
    /// Toplam tutar
    /// </summary>
    public decimal GrandTotal { get; set; }

    /// <summary>
    /// Kalan tutar
    /// </summary>
    public decimal RemainingSum { get; set; }

    /// <summary>
    /// Para birimi
    /// </summary>
    public string? Currency { get; set; }

    /// <summary>
    /// Fiyat degisti mi?
    /// </summary>
    public bool IsPriceChanged { get; set; }

    /// <summary>
    /// Rezervasyon iptal edildi mi?
    /// </summary>
    public bool IsReservationCancelled { get; set; }

    /// <summary>
    /// Yolcu bilgileri
    /// </summary>
    public List<ReadShoppingPassenger> Passengers { get; set; } = [];

    /// <summary>
    /// Segment bilgileri
    /// </summary>
    public List<PreBookingSegment> Segments { get; set; } = [];

    /// <summary>
    /// E-bilet numaralari
    /// </summary>
    public List<TicketInfo> Tickets { get; set; } = [];

    /// <summary>
    /// Odeme bilgileri
    /// </summary>
    public List<ReadShoppingPayment> Payments { get; set; } = [];
}

public class ReadShoppingPassenger
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Type { get; set; }
    public string? Gender { get; set; }
    public string? CitizenNo { get; set; }
    public string? TicketNumber { get; set; }
    public int SequenceNo { get; set; }
}

public class ReadShoppingPayment
{
    public string? PaymentType { get; set; }
    public decimal Amount { get; set; }
    public string? Currency { get; set; }
    public string? Status { get; set; }
    public string? ReferenceId { get; set; }
}
