namespace GBILET.Core.Entities;

/// <summary>
/// Üye olmadan bilet alan misafir kullanýcýlarý temsil eder.
/// Her bilet iþleminde benzersiz bir GUID atanýr; ileride admin panelinden takip edilebilir.
/// </summary>
public class GuestSession
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Misafir kullanýcýnýn e-posta adresi (iletiþim bilgisinden alýnýr)
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Misafir kullanýcýnýn telefon numarasý
    /// </summary>
    public string? Phone { get; set; }

    /// <summary>
    /// Ýstek yapan IP adresi
    /// </summary>
    public string? IpAddress { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public List<Booking> Bookings { get; set; } = new();
}
