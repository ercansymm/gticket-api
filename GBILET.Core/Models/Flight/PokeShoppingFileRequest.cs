namespace GBILET.Core.Models.Flight;

public class PokeShoppingFileRequest
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
    /// Durumu sorgulanacak shopping dosya ID'si.
    /// </summary>
    public string ShoppingFileId { get; set; } = null!;
}
