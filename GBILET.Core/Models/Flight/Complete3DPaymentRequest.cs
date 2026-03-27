namespace GBILET.Core.Models.Flight;

/// <summary>
/// 3D Secure dogrulamasi tamamlandiktan sonra bankanin callback'te gonderdigi verileri tasir.
/// </summary>
public class Complete3DPaymentRequest
{
    /// <summary>
    /// Aktif oturum bilgisi. 3D baslatilmadan onceki SessionId.
    /// </summary>
    public string SessionId { get; set; } = null!;

    /// <summary>
    /// Aktif oturum bilgisi. 3D baslatilmadan onceki SessionToken.
    /// </summary>
    public string SessionToken { get; set; } = null!;

    /// <summary>
    /// MakePreBooking'den alinan ShoppingFileId.
    /// </summary>
    public string ShoppingFileId { get; set; } = null!;

    /// <summary>
    /// Bankadan gelen tum form parametreleri (MD, PaRes, vb.)
    /// BiletBank'a oldugu gibi iletilir.
    /// </summary>
    public Dictionary<string, string> BankResponseParameters { get; set; } = new();

    /// <summary>
    /// DB'deki booking ID'si.
    /// </summary>
    public Guid? BookingId { get; set; }
}
