namespace GBILET.Infrastructure.Entity;

public class TripBooking
{
    public Guid Id { get; set; }
    public Guid TripId { get; set; }
    public Guid BookingId { get; set; }

    public Trip Trip { get; set; }
    public Booking Booking { get; set; }
}
