namespace GBILET.Core.Service.Flight;

public class BookingRequest
{
    /// <summary>
    /// Allocate response'tan alinan SessionId
    /// </summary>
    public string SessionId { get; set; } = null!;

    /// <summary>
    /// Allocate response'tan alinan SessionToken
    /// </summary>
    public string SessionToken { get; set; } = null!;

    /// <summary>
    /// Allocate response'tan alinan ShoppingFileId
    /// </summary>
    public string ShoppingFileId { get; set; } = null!;

    /// <summary>
    /// Allocate response'tan alinan ProductId (T_AirBooking.ProductId)
    /// </summary>
    public string ProductId { get; set; } = null!;

    /// <summary>
    /// Allocate response'tan alinan ProductItemId (T_AirBookingItem.ProductItemId)
    /// </summary>
    public string ProductItemId { get; set; } = null!;

    /// <summary>
    /// Yolcu listesi (ADT, CHD, INF sýrasýna göre)
    /// </summary>
    public List<BookingPassenger> Passengers { get; set; } = [];

    /// <summary>
    /// Rezervasyon sahibi iletisim bilgileri
    /// </summary>
    public BookingContact Contact { get; set; } = null!;
}

/// <summary>
/// Booking icin yolcu bilgisi
/// </summary>
public class BookingPassenger
{
    /// <summary>
    /// Yolcu tipi: ADT (yetiskin), CHD (cocuk), INF (bebek)
    /// </summary>
    public string PaxType { get; set; } = "ADT";

    /// <summary>
    /// Allocate response'taki PaxSequenceNo ile eslesmeli (1, 2, 3...)
    /// </summary>
    public int SequenceNo { get; set; }

    /// <summary>
    /// Yolcunun adi
    /// </summary>
    public string FirstName { get; set; } = null!;

    /// <summary>
    /// Yolcunun soyadi
    /// </summary>
    public string LastName { get; set; } = null!;

    /// <summary>
    /// Cinsiyet: M (Male), F (Female)
    /// </summary>
    public string Gender { get; set; } = null!;

    /// <summary>
    /// Dogum tarihi (yyyy-MM-dd)
    /// </summary>
    public string BirthDate { get; set; } = null!;

    /// <summary>
    /// TC Kimlik No (yurt ici ucuslar icin)
    /// </summary>
    public string? CitizenNo { get; set; }

    /// <summary>
    /// Pasaport numarasi (yurt disi ucuslar icin)
    /// </summary>
    public string? PassportNo { get; set; }

    /// <summary>
    /// Pasaport ulke kodu (ornegin: TR)
    /// </summary>
    public string? PassportCountry { get; set; }

    /// <summary>
    /// Pasaport bitis tarihi (yyyy-MM-dd)
    /// </summary>
    public string? PassportExpiry { get; set; }

    /// <summary>
    /// Uyruk kodu (ornegin: TR)
    /// </summary>
    public string Nationality { get; set; } = "TR";
}

/// <summary>
/// Rezervasyon sahibi iletisim bilgisi
/// </summary>
public class BookingContact
{
    /// <summary>
    /// E-posta adresi
    /// </summary>
    public string Email { get; set; } = null!;

    /// <summary>
    /// Telefon numarasi (ulke kodu dahil: +905xxxxxxxxx)
    /// </summary>
    public string Phone { get; set; } = null!;
}
