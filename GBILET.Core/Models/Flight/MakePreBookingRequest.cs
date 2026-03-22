namespace GBILET.Core.Models.Flight;

public class MakePreBookingRequest
{
    /// <summary>
    /// Search/Allocate adimindan alinan SessionId.
    /// </summary>
    public string SessionId { get; set; } = null!;

    /// <summary>
    /// Search/Allocate adimindan alinan SessionToken.
    /// </summary>
    public string SessionToken { get; set; } = null!;

    /// <summary>
    /// Allocate response'taki AirBookings[0].ProductId degeri.
    /// </summary>
    public string ProductId { get; set; } = null!;

    /// <summary>
    /// Allocate response'taki AirBookings[0].BrandedFareItems[0].BrandedFareItemId degeri.
    /// </summary>
    public string BrandedFareItemId { get; set; } = null!;

    /// <summary>
    /// Allocate response'taki ShoppingFileId degeri.
    /// </summary>
    public string ShoppingFileId { get; set; } = null!;

    /// <summary>
    /// Kayitli kullanici ID'si — opsiyonel, verilmezse misafir oturumu olusturulur.
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// Yolcu listesi — DB'ye booking kaydý olusturmak icin gerekli.
    /// </summary>
    public List<UpdatePassengerItem> Passengers { get; set; } = [];

    /// <summary>
    /// Rezervasyon sahibi iletisim bilgileri — DB kaydý icin gerekli.
    /// </summary>
    public UpdatePassengerContact Contact { get; set; } = null!;
}
