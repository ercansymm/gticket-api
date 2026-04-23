namespace GBILET.Core.Models.Flight;

/// <summary>
/// Tek request ile t�m u�u� rezervasyon ak���n� ba�latan model.
/// Search ? Allocate ? UpdatePassengers ? MakePreBooking ? MakePayment ? FinalizeShopping
/// </summary>
public class BookFlightRequest
{
    // ?? U�u� Arama ??
    public string Origin { get; set; } = null!;
    public string Destination { get; set; } = null!;
    public DateTime DepartureDate { get; set; }
    public DateTime? ReturnDate { get; set; }
    public string FlightType { get; set; } = "OW";
    public string FlightClass { get; set; } = "Economy";
    public int AdultCount { get; set; } = 1;
    public int ChildCount { get; set; } = 0;
    public int InfantCount { get; set; } = 0;

    // ?? U�u� Se�imi ??
    /// <summary>
    /// Hangi u�u�u se�mek istedi�inizi belirler.
    /// 0 = en ucuz u�u� (varsay�lan).
    /// </summary>
    public int FlightIndex { get; set; } = 0;

    /// <summary>
    /// Branded fare paketi indexi.
    /// 0 = en ucuz paket (varsay�lan).
    /// </summary>
    public int BrandedFareIndex { get; set; } = 0;

    // ?? Yolcu Bilgileri ??
    public List<BookFlightPassenger> Passengers { get; set; } = [];

    // ?? �leti�im ??
    public string ContactEmail { get; set; } = null!;
    public string ContactPhone { get; set; } = null!;


    // ?? Ödeme ??
    /// <summary>
    /// "CreditCard" veya "CreditCardDirect"
    /// </summary>
    public string PaymentType { get; set; } = "CreditCard";

    /// <summary>
    /// PaymentType = CreditCard ise zorunlu.
    /// </summary>
    public CreditCardInfo? CreditCard { get; set; }

    // ?? Kullan�c� (opsiyonel) ??
    public Guid? UserId { get; set; }

    /// <summary>
    /// true ise �deme ve biletleme de yap�l�r.
    /// false ise sadece PreBooking'e kadar gidilir (�deme yap�lmaz).
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
