namespace GBILET.Core.Models.Flight;

/// <summary>
/// 3D Secure baslatildiginda session bilgilerini cache'lemek icin kullanilir.
/// Callback geldiginde bu bilgiler gereklidir.
/// </summary>
public class ThreeDSessionData
{
    public string SessionId { get; set; } = null!;
    public string SessionToken { get; set; } = null!;
    public string ShoppingFileId { get; set; } = null!;
    public Guid? BookingId { get; set; }
    public string? ProductId { get; set; }
    public ShoppingBillingInfo? BillingInfo { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? Nonce { get; set; }
    public string? FrontendBaseUrl { get; set; }
}
