namespace GBILET.Core.Models.Flight;

/// <summary>
/// Otomatik booking ak���n�n her ad�m�n�n sonucunu d�ner.
/// Hangi ad�mda durdu�u, ba�ar�l� m�, hata varsa ne oldu�u bilgisi i�erir.
/// </summary>
public class BookFlightResponse
{
    public bool HasError { get; set; }
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Son tamamlanan ad�m: Search, Allocate, UpdatePassengers, PreBooking, Payment, Finalize
    /// </summary>
    public string? CompletedStep { get; set; }

    // ?? Search ??
    public string? SessionId { get; set; }
    public string? SessionToken { get; set; }
    public int TotalFlightsFound { get; set; }

    // ?? Allocate ??
    public string? ShoppingFileId { get; set; }
    public string? ProductId { get; set; }
    public string? ProductItemId { get; set; }
    public string? BrandedFareItemId { get; set; }

    // ?? PreBooking ??
    public string? PNR { get; set; }
    public string? InternalPnr { get; set; }
    public string? Status { get; set; }
    public decimal TotalFare { get; set; }
    public decimal BaseFare { get; set; }
    public decimal Taxes { get; set; }
    public decimal ServiceFee { get; set; }
    public string? Currency { get; set; }
    public Guid? BookingId { get; set; }
    public DateTime? PrebookingExpiresAt { get; set; }
    public DateTime? ReservationExpiresAt { get; set; }

    // ?? Payment ??
    public bool IsPaymentSuccessful { get; set; }
    public string? PaymentReferenceId { get; set; }
    public string? ThreeDSecureUrl { get; set; }
    public bool Is3DSecureRequired { get; set; }

    /// <summary>
    /// 3D Secure HTML icerik (banka 3D dogrulama formu).
    /// Bu HTML'i kullaniciya iframe veya yeni pencerede gosterin.
    /// </summary>
    public string? ThreeDSecureHtml { get; set; }

    // ?? Finalize ??
    public bool IsFinalized { get; set; }
    public List<TicketInfo> Tickets { get; set; } = [];

    // ?? Se�ilen u�u� bilgisi ??
    public string? FlightNumber { get; set; }
    public string? MarketingAirline { get; set; }
    public string? Origin { get; set; }
    public string? Destination { get; set; }
    public string? DepartureDay { get; set; }
    public string? DepartureTime { get; set; }

    // ?? Ad�m detaylar� (debug) ??
    public List<string> Steps { get; set; } = [];
}
