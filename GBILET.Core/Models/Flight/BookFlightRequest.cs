namespace GBILET.Core.Models.Flight;

/// <summary>
/// Tek request ile tüm uçuþ rezervasyon akýþýný baþlatan model.
/// Search ? Allocate ? UpdatePassengers ? MakePreBooking ? MakePayment ? FinalizeShopping
/// </summary>
public class BookFlightRequest
{
    // ?? Uçuþ Arama ??
    public string Origin { get; set; } = null!;
    public string Destination { get; set; } = null!;
    public DateTime DepartureDate { get; set; }
    public DateTime? ReturnDate { get; set; }
    public string FlightType { get; set; } = "OW";
    public string FlightClass { get; set; } = "Economy";
    public int AdultCount { get; set; } = 1;
    public int ChildCount { get; set; } = 0;
    public int InfantCount { get; set; } = 0;

    // ?? Uçuþ Seçimi ??
    /// <summary>
    /// Hangi uçuþu seçmek istediðinizi belirler.
    /// 0 = en ucuz uçuþ (varsayýlan).
    /// </summary>
    public int FlightIndex { get; set; } = 0;

    /// <summary>
    /// Branded fare paketi indexi.
    /// 0 = en ucuz paket (varsayýlan).
    /// </summary>
    public int BrandedFareIndex { get; set; } = 0;

    // ?? Yolcu Bilgileri ??
    public List<BookFlightPassenger> Passengers { get; set; } = [];

    // ?? Ýletiþim ??
    public string ContactEmail { get; set; } = null!;
    public string ContactPhone { get; set; } = null!;

    // ?? Ödeme ??
    /// <summary>
    /// "RunningAccount" veya "CreditCard"
    /// </summary>
    public string PaymentType { get; set; } = "RunningAccount";

    /// <summary>
    /// PaymentType = CreditCard ise zorunlu.
    /// </summary>
    public CreditCardInfo? CreditCard { get; set; }

    // ?? Kullanýcý (opsiyonel) ??
    public Guid? UserId { get; set; }

    /// <summary>
    /// true ise ödeme ve biletleme de yapýlýr.
    /// false ise sadece PreBooking'e kadar gidilir (ödeme yapýlmaz).
    /// </summary>
    public bool AutoPayAndFinalize { get; set; } = true;
}

public class BookFlightPassenger
{
    public string PaxType { get; set; } = "ADT";
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string Gender { get; set; } = null!;
    public string BirthDate { get; set; } = null!;
    public string? CitizenNo { get; set; }
    public string? PassportNo { get; set; }
    public string? PassportCountry { get; set; }
    public string Nationality { get; set; } = "TR";
}
