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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Booking
        modelBuilder.Entity<Booking>(e =>
        {
            e.HasKey(b => b.Id);
            e.HasIndex(b => b.PNR);
            e.HasIndex(b => b.Status);
            e.HasOne(b => b.User).WithMany().HasForeignKey(b => b.UserId).IsRequired(false);
            e.HasMany(b => b.Passengers).WithOne(p => p.Booking).HasForeignKey(p => p.BookingId);
            e.HasMany(b => b.FlightSegments).WithOne(s => s.Booking).HasForeignKey(s => s.BookingId);
            e.HasMany(b => b.Payments).WithOne(p => p.Booking).HasForeignKey(p => p.BookingId);
            e.HasMany(b => b.FareDetails).WithOne(f => f.Booking).HasForeignKey(f => f.BookingId);
            e.HasOne(b => b.BillingInfo).WithOne(bi => bi.Booking).HasForeignKey<BillingInfo>(bi => bi.BookingId);
            e.HasMany(b => b.BookingLogs).WithOne(l => l.Booking).HasForeignKey(l => l.BookingId);
        });

        // Trip
        modelBuilder.Entity<Trip>(e =>
        {
            e.HasKey(t => t.Id);
            e.HasOne(t => t.User).WithMany().HasForeignKey(t => t.UserId);
        });

        // TripBooking
        modelBuilder.Entity<TripBooking>(e =>
        {
            e.HasKey(tb => tb.Id);
            e.HasOne(tb => tb.Trip).WithMany().HasForeignKey(tb => tb.TripId);
            e.HasOne(tb => tb.Booking).WithMany().HasForeignKey(tb => tb.BookingId);
        });

        // TripPassenger
        modelBuilder.Entity<TripPassenger>(e =>
        {
            e.HasKey(tp => tp.Id);
            e.HasOne(tp => tp.Trip).WithMany().HasForeignKey(tp => tp.TripId);
            e.HasOne(tp => tp.Passenger).WithMany().HasForeignKey(tp => tp.PassengerId);
        });

        // BookingLog
        modelBuilder.Entity<BookingLog>(e =>
        {
            e.HasKey(l => l.Id);
            e.HasIndex(l => l.Operation);
            e.HasIndex(l => l.CreatedAt);
        });

        // SystemLog
        modelBuilder.Entity<SystemLog>(e =>
        {
            e.HasKey(l => l.Id);
            e.HasIndex(l => l.Action);
            e.HasIndex(l => l.CreatedAt);
            e.HasOne(l => l.User).WithMany().HasForeignKey(l => l.UserId);
        });

        // User
        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(u => u.Id);
            e.HasIndex(u => u.Email).IsUnique();
            e.HasIndex(u => u.CustomerNumber).IsUnique();
        });
    }
}