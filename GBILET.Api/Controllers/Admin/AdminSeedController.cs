using GBILET.Core.Entities;
using GBILET.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace GBILET.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/seed")]
[Authorize]
[EnableRateLimiting("admin-general")]
public class AdminSeedController : ControllerBase
{
    private readonly GTicketDbContext _db;

    public AdminSeedController(GTicketDbContext db)
    {
        _db = db;
    }

    // POST /api/admin/seed/bookings
    [HttpPost("bookings")]
    public async Task<IActionResult> SeedBookings(CancellationToken ct)
    {
        var hasBookings = await _db.Bookings.AnyAsync(ct);
        if (hasBookings)
            return Ok(new { message = "Already seeded, skipped", count = 0 });

        var rng = new Random(42);
        var now = DateTime.UtcNow;
        var firstNames = new[] { "Ahmet", "Mehmet", "Fatma", "Zeynep", "Ali", "Ayşe", "Mustafa", "Elif", "Hasan", "Merve" };
        var lastNames = new[] { "Yılmaz", "Kaya", "Demir", "Arslan", "Şahin", "Koç", "Öztürk", "Aydın", "Yıldız", "Çelik" };
        var origins = new[] { "IST", "SAW", "ESB", "ADB", "JFK", "DXB" };
        var destinations = new[] { "IST", "ESB", "ADB", "LHR", "JFK", "DXB" };
        var airlines = new[] { "TK", "PC", "XQ" };

        var seedData = new (string Status, bool IsFinalized, int DaysAgo)[]
        {
            ("Ticketed", true, 5),
            ("Ticketed", true, 10),
            ("Ticketed", true, 33),
            ("Ticketed", true, 40),
            ("Paid", true, 10),
            ("Paid", true, 14),
            ("PreBooked", false, 30),
            ("PreBooked", false, 33),
            ("Cancelled", false, 17),
            ("PaymentFailed", false, 21),
        };

        var bookings = new List<Booking>();

        for (int i = 0; i < seedData.Length; i++)
        {
            var (status, isFinalized, daysAgo) = seedData[i];
            var createdAt = now.AddDays(-daysAgo).AddHours(rng.Next(-12, 12));
            var pnr = $"ATB{rng.Next(100000, 999999)}";
            var origin = origins[rng.Next(origins.Length)];
            var dest = destinations.Where(d => d != origin).ElementAt(rng.Next(destinations.Length - 1));
            var airline = airlines[rng.Next(airlines.Length)];
            var adultCount = rng.Next(1, 3);
            var childCount = rng.Next(0, 2);
            var infantCount = rng.Next(0, 2);
            var totalPax = adultCount + childCount + infantCount;

            var booking = new Booking
            {
                Id = Guid.NewGuid(),
                PNR = pnr,
                InternalPnr = pnr,
                Status = status,
                IsFinalized = isFinalized,
                GrandTotal = Math.Round((decimal)(rng.NextDouble() * 20000 + 1000), 2),
                Currency = "TRY",
                Origin = origin,
                Destination = dest,
                AirlineCode = airline,
                FlightNumber = $"{airline}{rng.Next(100, 9999)}",
                ServiceFee = Math.Round((decimal)(rng.NextDouble() * 100 + 20), 2),
                OurCommission = Math.Round((decimal)(rng.NextDouble() * 800 + 50), 2),
                AdultCount = adultCount,
                ChildCount = childCount,
                InfantCount = infantCount,
                CreatedAt = createdAt,
                UpdatedAt = createdAt.AddMinutes(rng.Next(1, 10)),
                BookedAt = status is "PreBooked" or "Paid" or "Ticketed" ? createdAt.AddMinutes(1) : null,
                PaidAt = status is "Paid" or "Ticketed" ? createdAt.AddMinutes(8) : null,
                TicketedAt = status == "Ticketed" ? createdAt.AddMinutes(15) : null,
                CancelledAt = status == "Cancelled" ? createdAt.AddMinutes(5) : null,
                LastError = status == "PaymentFailed" ? "CardInformationIsNotValid" : null,
            };

            // Add passengers
            int seq = 1;
            for (int p = 0; p < totalPax; p++)
            {
                string paxType;
                if (p < adultCount) paxType = "ADT";
                else if (p < adultCount + childCount) paxType = "CHD";
                else paxType = "INF";

                var year = paxType switch
                {
                    "ADT" => rng.Next(1970, 2000),
                    "CHD" => rng.Next(2015, 2022),
                    _ => rng.Next(2023, 2026)
                };

                booking.Passengers.Add(new Passenger
                {
                    Id = Guid.NewGuid(),
                    BookingId = booking.Id,
                    SequenceNo = seq++,
                    Type = paxType,
                    FirstName = firstNames[rng.Next(firstNames.Length)],
                    LastName = lastNames[rng.Next(lastNames.Length)],
                    Gender = rng.Next(2) == 0 ? "M" : "F",
                    BirthDate = new DateTime(year, rng.Next(1, 13), rng.Next(1, 28)).ToString("yyyy-MM-dd"),
                    Email = p == 0 ? $"{firstNames[rng.Next(firstNames.Length)].ToLower()}.{lastNames[rng.Next(lastNames.Length)].ToLower()}@example.com" : null,
                    Phone = p == 0 ? $"+90{rng.Next(500, 560)}{rng.Next(1000000, 9999999)}" : null,
                });
            }

            // Add a seed log
            booking.BookingLogs.Add(new BookingLog
            {
                Id = Guid.NewGuid(),
                BookingId = booking.Id,
                Operation = "Seed",
                IsSuccess = true,
                CreatedAt = createdAt,
            });

            bookings.Add(booking);
        }

        _db.Bookings.AddRange(bookings);
        await _db.SaveChangesAsync(ct);

        return Ok(new { message = "Seeded", count = bookings.Count });
    }
}
