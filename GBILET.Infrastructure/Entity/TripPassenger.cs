namespace GBILET.Infrastructure.Entity;

public class TripPassenger
{
    public Guid Id { get; set; }
    public Guid TripId { get; set; }
    public Guid PassengerId { get; set; }

    public Trip Trip { get; set; }
    public Passenger Passenger { get; set; }
}
