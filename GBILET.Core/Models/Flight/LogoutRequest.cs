namespace GBILET.Core.Models.Flight;

public class LogoutRequest
{
    /// <summary>
    /// Kapatilacak oturumun SessionId'si.
    /// </summary>
    public string SessionId { get; set; } = null!;

    /// <summary>
    /// Kapatilacak oturumun SessionToken'i.
    /// </summary>
    public string SessionToken { get; set; } = null!;
}
