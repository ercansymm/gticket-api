using Microsoft.EntityFrameworkCore;
using GBILET.Core.Entities;

namespace GBILET.Infrastructure.Data;

public class GTicketDbContext : DbContext
{
    public GTicketDbContext(DbContextOptions<GTicketDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Booking> Bookings { get; set; }
    public DbSet<Passenger> Passengers { get; set; }
    public DbSet<FlightSegment> FlightSegments { get; set; }
    public DbSet<Payment> Payments { get; set; }
    public DbSet<FareDetail> FareDetails { get; set; }
    public DbSet<BillingInfo> BillingInfo { get; set; }
    public DbSet<Trip> Trips { get; set; }
    public DbSet<TripBooking> TripBookings { get; set; }
    public DbSet<TripPassenger> TripPassengers { get; set; }
    public DbSet<BookingLog> BookingLogs { get; set; }
    public DbSet<SystemLog> SystemLogs { get; set; }
}