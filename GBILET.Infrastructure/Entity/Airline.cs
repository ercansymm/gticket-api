namespace GBILET.Infrastructure.Entity;

public class Airline
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameTr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string? CountryCode { get; set; }
    public string? Alliance { get; set; }
    public bool IsDomestic { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
