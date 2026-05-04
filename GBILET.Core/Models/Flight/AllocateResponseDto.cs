namespace GBILET.Core.Models.Flight;

/// <summary>
/// Allocate endpoint icin frontend-friendly response wrapper. Mevcut AllocateResponse
/// ile yan yana yasar; controller bu DTO'yu opsiyonel olarak donebilir.
/// </summary>
public class AllocateResponseDto
{
    /// <summary>Provider'dan donen taze allocate yaniti (mevcut AllocateResponse seklini korur).</summary>
    public AllocateResponse Allocate { get; set; } = new();

    /// <summary>Cache snapshot ile karsilastirma yapildiktan sonra olusturulan ozet DTO.</summary>
    public FlightResultDto? Flight { get; set; }

    public bool HasChanges { get; set; }
    public bool CanProceedToCheckout { get; set; }
    public string? ChangeType { get; set; }
    public string? UserMessage { get; set; }

    public decimal? OldPrice { get; set; }
    public decimal? NewPrice { get; set; }
    public string? OldDepartureTime { get; set; }
    public string? NewDepartureTime { get; set; }
}
