
using Microsoft.EntityFrameworkCore;
using GBILET.Core.Entities;

namespace GBILET.Infrastructure.Data;

public class GTicketDbContext : DbContext
{
    public GTicketDbContext(DbContextOptions<GTicketDbContext> options) : base(options)
    {
    }

    public override int SaveChanges()
    {
        ConvertDateTimesToUtc();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ConvertDateTimesToUtc();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void ConvertDateTimesToUtc()
    {
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
                continue;

            foreach (var prop in entry.Properties)
            {
                if (prop.CurrentValue is DateTime dt && dt.Kind == DateTimeKind.Unspecified)
                {
                    prop.CurrentValue = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                }
            }
        }
    }

    public DbSet<User> Users { get; set; }
    public DbSet<GuestSession> GuestSessions { get; set; }
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
    public DbSet<Airport> Airports { get; set; }
    public DbSet<Airline> Airlines { get; set; }
    public DbSet<FareType> FareTypes { get; set; }
    public DbSet<BookingClass> BookingClasses { get; set; }
    public DbSet<Session> Sessions { get; set; }
    public DbSet<SearchLog> SearchLogs { get; set; }
    public DbSet<PopularRoute> PopularRoutes { get; set; }

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
            e.HasOne(b => b.GuestSession).WithMany(g => g.Bookings).HasForeignKey(b => b.GuestSessionId).IsRequired(false);
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

        // GuestSession
        modelBuilder.Entity<GuestSession>(e =>
        {
            e.HasKey(g => g.Id);
            e.HasIndex(g => g.Email);
        });

        // User
        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(u => u.Id);
            e.HasIndex(u => u.Email).IsUnique();
            e.HasIndex(u => u.CustomerNumber).IsUnique();
        });

        // Airport
        modelBuilder.Entity<Airport>(e =>
        {
            e.HasKey(a => a.Id);
            e.Property(a => a.IataCode).HasMaxLength(3).IsRequired();
            e.Property(a => a.IcaoCode).HasMaxLength(4);
            e.Property(a => a.NameTr).HasMaxLength(100).IsRequired();
            e.Property(a => a.NameEn).HasMaxLength(100).IsRequired();
            e.Property(a => a.CityTr).HasMaxLength(50).IsRequired();
            e.Property(a => a.CityEn).HasMaxLength(50).IsRequired();
            e.Property(a => a.CountryTr).HasMaxLength(50).IsRequired();
            e.Property(a => a.CountryEn).HasMaxLength(50).IsRequired();
            e.Property(a => a.CountryCode).HasMaxLength(2).IsRequired();
            e.Property(a => a.Timezone).HasMaxLength(50);
            e.Property(a => a.CityCode).HasMaxLength(5);
            e.Property(a => a.Type).HasMaxLength(20);
            e.Property(a => a.Region).HasMaxLength(50);
            e.HasIndex(a => a.IataCode).IsUnique();
            e.HasIndex(a => a.CountryCode);
            e.HasIndex(a => a.IsDomestic);
            e.HasIndex(a => a.IsPopular);
        });

        // Airline
        modelBuilder.Entity<Airline>(e =>
        {
            e.HasKey(a => a.Id);
            e.Property(a => a.Code).HasMaxLength(3).IsRequired();
            e.Property(a => a.NameTr).HasMaxLength(100).IsRequired();
            e.Property(a => a.NameEn).HasMaxLength(100).IsRequired();
            e.Property(a => a.LogoUrl).HasMaxLength(255);
            e.Property(a => a.CountryCode).HasMaxLength(2);
            e.Property(a => a.Alliance).HasMaxLength(50);
            e.HasIndex(a => a.Code).IsUnique();
        });

        // FareType
        modelBuilder.Entity<FareType>(e =>
        {
            e.HasKey(f => f.Id);
            e.Property(f => f.Code).HasMaxLength(10).IsRequired();
            e.Property(f => f.NameTr).HasMaxLength(50).IsRequired();
            e.Property(f => f.NameEn).HasMaxLength(50).IsRequired();
            e.HasIndex(f => f.Code).IsUnique();
            e.HasMany(f => f.BookingClasses).WithOne(bc => bc.FareType).HasForeignKey(bc => bc.FareTypeId);
        });

        // BookingClass
        modelBuilder.Entity<BookingClass>(e =>
        {
            e.HasKey(bc => bc.Id);
            e.Property(bc => bc.Code).HasMaxLength(2).IsRequired();
            e.Property(bc => bc.NameTr).HasMaxLength(50).IsRequired();
            e.Property(bc => bc.NameEn).HasMaxLength(50).IsRequired();
            e.HasIndex(bc => bc.Code).IsUnique();
        });

        // Session
        modelBuilder.Entity<Session>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.BiletBankSessionId).HasMaxLength(100);
            e.Property(s => s.BiletBankSessionToken).HasMaxLength(100);
            e.Property(s => s.ShoppingFileId).HasMaxLength(100);
            e.Property(s => s.IpAddress).HasMaxLength(50);
            e.Property(s => s.Status).HasMaxLength(20);
            e.HasOne(s => s.User).WithMany().HasForeignKey(s => s.UserId).IsRequired(false);
        });

        // SearchLog
        modelBuilder.Entity<SearchLog>(e =>
        {
            e.HasKey(sl => sl.Id);
            e.Property(sl => sl.TransactionId).HasMaxLength(100);
            e.Property(sl => sl.Origin).HasMaxLength(3).IsRequired();
            e.Property(sl => sl.Destination).HasMaxLength(3).IsRequired();
            e.Property(sl => sl.FlightType).HasMaxLength(5);
            e.Property(sl => sl.FlightClass).HasMaxLength(20);
            e.Property(sl => sl.MinPrice).HasColumnType("decimal(18,2)");
            e.Property(sl => sl.ErrorMessage).HasMaxLength(500);
            e.Property(sl => sl.IpAddress).HasMaxLength(50);
            e.HasIndex(sl => sl.CreatedAt);
            e.HasOne(sl => sl.Session).WithMany().HasForeignKey(sl => sl.SessionId).IsRequired(false);
        });

        // PopularRoute
        modelBuilder.Entity<PopularRoute>(e =>
        {
            e.HasKey(pr => pr.Id);
            e.Property(pr => pr.OriginCode).HasMaxLength(3).IsRequired();
            e.Property(pr => pr.DestinationCode).HasMaxLength(3).IsRequired();
            e.Property(pr => pr.DisplayPrice).HasColumnType("decimal(18,2)");
            e.Property(pr => pr.Currency).HasMaxLength(3);
        });

        // FareDetail decimal configuration
        modelBuilder.Entity<FareDetail>(e =>
        {
            e.Property(f => f.BaseFare).HasColumnType("decimal(18,2)");
            e.Property(f => f.TotalTax).HasColumnType("decimal(18,2)");
            e.Property(f => f.ServiceFee).HasColumnType("decimal(18,2)");
            e.Property(f => f.GrandTotal).HasColumnType("decimal(18,2)");
            e.Property(f => f.BiletBankCost).HasColumnType("decimal(18,2)");
            e.Property(f => f.OurPrice).HasColumnType("decimal(18,2)");
            e.Property(f => f.Profit).HasColumnType("decimal(18,2)");
        });

        // Booking new fields configuration
        modelBuilder.Entity<Booking>(e2 =>
        {
            e2.Property(b => b.TransactionId).HasMaxLength(100);
            e2.Property(b => b.Origin).HasMaxLength(3);
            e2.Property(b => b.Destination).HasMaxLength(3);
            e2.Property(b => b.AirlineCode).HasMaxLength(3);
            e2.Property(b => b.FlightNumber).HasMaxLength(20);
            e2.Property(b => b.SessionId).HasMaxLength(100);
            e2.Property(b => b.SessionToken).HasMaxLength(100);
            e2.Property(b => b.ProductItemId).HasMaxLength(100);
            e2.Property(b => b.ServiceFee).HasColumnType("decimal(18,2)");
            e2.Property(b => b.OurCommission).HasColumnType("decimal(18,2)");
        });

        // Payment new fields configuration
        modelBuilder.Entity<Payment>(e2 =>
        {
            e2.Property(p => p.Amount).HasColumnType("decimal(18,2)");
            e2.Property(p => p.CardLastFour).HasMaxLength(4);
            e2.Property(p => p.CardHolder).HasMaxLength(100);
            e2.Property(p => p.ProviderTransactionId).HasMaxLength(100);
            e2.Property(p => p.ErrorMessage).HasMaxLength(500);
            e2.Property(p => p.RefundAmount).HasColumnType("decimal(18,2)");
        });

        // Passenger new fields configuration
        modelBuilder.Entity<Passenger>(e2 =>
        {
            e2.Property(p => p.TempTag).HasMaxLength(100);
            e2.Property(p => p.PaxReferenceId).HasMaxLength(100);
            e2.Property(p => p.TicketNumber).HasMaxLength(20);
        });

        // Seed Data
        DataSeeder.Seed(modelBuilder);
    }
}