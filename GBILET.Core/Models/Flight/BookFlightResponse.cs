namespace GBILET.Core.Models.Flight;

/// <summary>
/// Otomatik booking akýþýnýn her adýmýnýn sonucunu döner.
/// Hangi adýmda durduðu, baþarýlý mý, hata varsa ne olduðu bilgisi içerir.
/// </summary>
public class BookFlightResponse
{
    public bool HasError { get; set; }
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Son tamamlanan adým: Search, Allocate, UpdatePassengers, PreBooking, Payment, Finalize
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

    // ?? Finalize ??
    public bool IsFinalized { get; set; }
    public List<TicketInfo> Tickets { get; set; } = [];

    // ?? Seçilen uçuþ bilgisi ??
    public string? FlightNumber { get; set; }
    public string? MarketingAirline { get; set; }
    public string? Origin { get; set; }
    public string? Destination { get; set; }
    public string? DepartureDay { get; set; }
    public string? DepartureTime { get; set; }

    // ?? Adým detaylarý (debug) ??
    public List<string> Steps { get; set; } = [];
}
