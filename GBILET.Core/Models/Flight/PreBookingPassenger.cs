namespace GBILET.Core.Models.Flight;

/// <summary>
/// On rezervasyon adiminda donen yolcu bilgisi
/// </summary>
public class PreBookingPassenger
{
    /// <summary>
    /// Yolcu adi
    /// </summary>
    public string? FirstName { get; set; }

    /// <summary>
    /// Yolcu soyadi
    /// </summary>
    public string? LastName { get; set; }

    /// <summary>
    /// Yolcu tipi (ADT/CHD/INF)
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// TC kimlik numarasi
    /// </summary>
    public string? CitizenNo { get; set; }

    /// <summary>
    /// Cinsiyet
    /// </summary>
    public string? Gender { get; set; }
}
