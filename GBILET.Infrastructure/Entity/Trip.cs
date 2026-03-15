namespace GBILET.Infrastructure.Entity;

public class Trip
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string? TripName { get; set; }
    public string TripType { get; set; } = "OneWay";
    public string Origin { get; set; }
    public string Destination { get; set; }
    public DateTime DepartureDate { get; set; }
    public DateTime? ReturnDate { get; set; }
    public int TotalPassengers { get; set; } = 1;
    public string Status { get; set; } = "Planned";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; }
}
