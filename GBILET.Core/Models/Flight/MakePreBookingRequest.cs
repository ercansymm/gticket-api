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
}
