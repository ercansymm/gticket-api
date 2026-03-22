namespace GBILET.Core.Models.Flight;

public class CancelBookingRequest
{
    /// <summary>
    /// Aktif SessionId.
    /// </summary>
    public string SessionId { get; set; } = null!;

    /// <summary>
    /// Aktif SessionToken.
    /// </summary>
    public string SessionToken { get; set; } = null!;

    /// <summary>
    /// Iptal edilecek ProductId.
    /// </summary>
    public string ProductId { get; set; } = null!;

    /// <summary>
    /// Allocate/MakePreBooking adimindan alinan ShoppingFileId.
    /// </summary>
    public string ShoppingFileId { get; set; } = null!;

    /// <summary>
    /// DB'deki booking ID'si (durum guncelleme icin).
    /// </summary>
    public Guid? BookingId { get; set; }
}

public class BookingStatusRequest
{
    /// <summary>
    /// DB'deki booking ID'si.
    /// </summary>
    public Guid? BookingId { get; set; }

    /// <summary>
    /// PNR kodu (BookingId yoksa PNR ile aranir).
    /// </summary>
    public string? PNR { get; set; }

    /// <summary>
    /// Canli durum sorgulamak icin SessionId (opsiyonel).
    /// </summary>
    public string? SessionId { get; set; }

    /// <summary>
    /// Canli durum sorgulamak icin SessionToken (opsiyonel).
    /// </summary>
    public string? SessionToken { get; set; }
}
