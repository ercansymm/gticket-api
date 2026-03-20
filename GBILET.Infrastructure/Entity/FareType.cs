namespace GBILET.Infrastructure.Entity;

public class FareType
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameTr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public int SortOrder { get; set; } = 0;
    public bool IsActive { get; set; } = true;

    // Navigation
    public List<BookingClass> BookingClasses { get; set; } = new();
}
