namespace GBILET.Core.Models.Flight;

public class UpdatePassengersRequest
{
    /// <summary>
    /// Allocate adimindan alinan SessionId.
    /// </summary>
    public string SessionId { get; set; } = null!;

    /// <summary>
    /// Allocate adimindan alinan SessionToken.
    /// </summary>
    public string SessionToken { get; set; } = null!;

    /// <summary>
    /// Allocate response'taki ShoppingFileId.
    /// </summary>
    public string ShoppingFileId { get; set; } = null!;

    /// <summary>
    /// Allocate response'taki AirBookings[0].ProductId.
    /// </summary>
    public string ProductId { get; set; } = null!;

    /// <summary>
    /// Allocate response'taki BookingItems[0].ProductItemId.
    /// </summary>
    public string ProductItemId { get; set; } = null!;

    /// <summary>
    /// Yolcu listesi (ADT, CHD, INF s�ras�na g�re).
    /// </summary>
    public List<UpdatePassengerItem> Passengers { get; set; } = [];

    /// <summary>
    /// Rezervasyon sahibi iletisim bilgileri.
    /// </summary>
    public UpdatePassengerContact Contact { get; set; } = null!;
}

public class UpdatePassengerItem
{
    /// <summary>
    /// Yolcu tipi: ADT, CHD, INF
    /// </summary>
    public string PaxType { get; set; } = "ADT";

    /// <summary>
    /// Allocate response'taki SequenceNo ile eslesmeli (1, 2, 3...)
    /// </summary>
    public int SequenceNo { get; set; }

    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;

    /// <summary>
    /// Cinsiyet: M veya F
    /// </summary>
    public string Gender { get; set; } = null!;

    /// <summary>
    /// Dogum tarihi (yyyy-MM-dd)
    /// </summary>
    public string BirthDate { get; set; } = null!;

    public string? CitizenNo { get; set; }
    public string? PassportNo { get; set; }
    public string? PassportCountry { get; set; }

    /// <summary>
    /// Pasaport gecerlilik tarihi (yyyy-MM-dd) — yurt disi ucuslar icin zorunlu
    /// </summary>
    public string? PassportExpiry { get; set; }
    public string Nationality { get; set; } = "TR";

    /// <summary>
    /// Allocate response'taki T_Passenger.TempTag degeri.
    /// </summary>
    public string? TempTag { get; set; }

    /// <summary>
    /// Allocate response'taki PaxReference.PaxReferenceId degeri.
    /// </summary>
    public string? PaxReferenceId { get; set; }
}

public class UpdatePassengerContact
{
    public string Email { get; set; } = null!;
    public string Phone { get; set; } = null!;
}
