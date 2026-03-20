namespace GBILET.Infrastructure.Entity;

public class Airport
{
    public int Id { get; set; }
    public string IataCode { get; set; } = string.Empty;
    public string? IcaoCode { get; set; }
    public string NameTr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string CityTr { get; set; } = string.Empty;
    public string CityEn { get; set; } = string.Empty;
    public string CountryTr { get; set; } = string.Empty;
    public string CountryEn { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public string? Timezone { get; set; }
    public bool IsCity { get; set; } = false;
    public bool IsDomestic { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
