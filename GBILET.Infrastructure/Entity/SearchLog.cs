namespace GBILET.Infrastructure.Entity;

public class SearchLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? SessionId { get; set; }
    public string? TransactionId { get; set; }
    public string Origin { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public DateTime DepartureDate { get; set; }
    public DateTime? ReturnDate { get; set; }
    public string? FlightType { get; set; }
    public string? FlightClass { get; set; }
    public int AdultCount { get; set; } = 1;
    public int ChildCount { get; set; } = 0;
    public int InfantCount { get; set; } = 0;
    public int ResultCount { get; set; } = 0;
    public decimal? MinPrice { get; set; }
    public int? ResponseTimeMs { get; set; }
    public bool HasError { get; set; } = false;
    public string? ErrorMessage { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Session? Session { get; set; }
}
