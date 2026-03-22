namespace GBILET.Core.Models.Flight;

public class RemoveProductRequest
{
    /// <summary>
    /// Allocate/MakePreBooking adimindan alinan SessionId.
    /// </summary>
    public string SessionId { get; set; } = null!;

    /// <summary>
    /// Allocate/MakePreBooking adimindan alinan SessionToken.
    /// </summary>
    public string SessionToken { get; set; } = null!;

    /// <summary>
    /// Kaldirmak istenen ProductId (AirBookings[0].ProductId).
    /// </summary>
    public string ProductId { get; set; } = null!;

    /// <summary>
    /// Allocate/MakePreBooking adimindan alinan ShoppingFileId.
    /// </summary>
    public string ShoppingFileId { get; set; } = null!;
}
