namespace GBILET.Core.Models.Flight;

/// <summary>
/// On rezervasyon adiminda donen segment bilgisi
/// </summary>
public class PreBookingSegment
{
    /// <summary>
    /// Segment ID
    /// </summary>
    public string? SegmentId { get; set; }

    /// <summary>
    /// Kalkis havaalani kodu
    /// </summary>
    public string? OriginCode { get; set; }

    /// <summary>
    /// Varis havaalani kodu
    /// </summary>
    public string? DestinationCode { get; set; }

    /// <summary>
    /// Kalkis tarihi (yyyy-MM-dd)
    /// </summary>
    public string? DepartureDay { get; set; }

    /// <summary>
    /// Kalkis saati (HH:mm)
    /// </summary>
    public string? DepartureTime { get; set; }

    /// <summary>
    /// Varis tarihi (yyyy-MM-dd)
    /// </summary>
    public string? ArrivalDay { get; set; }

    /// <summary>
    /// Varis saati (HH:mm)
    /// </summary>
    public string? ArrivalTime { get; set; }

    /// <summary>
    /// Ucus numarasi
    /// </summary>
    public string? FlightNumber { get; set; }

    /// <summary>
    /// Pazarlama havayolu kodu
    /// </summary>
    public string? MarketingAirline { get; set; }

    /// <summary>
    /// Rezervasyon sinifi
    /// </summary>
    public string? BookingClass { get; set; }
}
