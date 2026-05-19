using GBILET.Core.DTOs.Admin;
using GBILET.Core.Helpers;
using GBILET.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace GBILET.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/dashboard")]
[Authorize]
[EnableRateLimiting("admin-general")]
public class AdminDashboardController : ControllerBase
{
    private readonly GTicketDbContext _db;

    private static readonly string[] SuccessStatuses =
        BookingStatusMapper.GetRawStatusesForCategory(BookingStatusCategory.Confirmed);

    public AdminDashboardController(GTicketDbContext db)
    {
        _db = db;
    }

    // GET /api/admin/dashboard/stats
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var startOfPrevMonth = startOfMonth.AddMonths(-1);

        var allBookings = await _db.Bookings
            .Where(b => b.IsFinalized || SuccessStatuses.Contains(b.Status))
            .Select(b => new { b.GrandTotal, b.CreatedAt, b.UserId })
            .ToListAsync(ct);

        var totalBookings = allBookings.Count;
        var thisMonthBookings = allBookings.Count(b => b.CreatedAt >= startOfMonth);
        var prevMonthBookings = allBookings.Count(b => b.CreatedAt >= startOfPrevMonth && b.CreatedAt < startOfMonth);

        var totalRevenue = allBookings.Sum(b => b.GrandTotal ?? 0);
        var thisMonthRevenue = allBookings.Where(b => b.CreatedAt >= startOfMonth).Sum(b => b.GrandTotal ?? 0);
        var prevMonthRevenue = allBookings.Where(b => b.CreatedAt >= startOfPrevMonth && b.CreatedAt < startOfMonth).Sum(b => b.GrandTotal ?? 0);

        var totalCustomers = await _db.Users.CountAsync(ct);
        var thisMonthCustomers = await _db.Users.CountAsync(u => u.CreatedAt >= startOfMonth, ct);
        var prevMonthCustomers = await _db.Users.CountAsync(u => u.CreatedAt >= startOfPrevMonth && u.CreatedAt < startOfMonth, ct);

        var activeUsers = await _db.Users.CountAsync(u => u.CreatedAt >= now.AddDays(-30), ct);

        return Ok(new DashboardStatsDto
        {
            TotalBookings = totalBookings,
            TotalBookingsThisMonth = thisMonthBookings,
            TotalRevenue = totalRevenue,
            TotalRevenueThisMonth = thisMonthRevenue,
            TotalCustomers = totalCustomers,
            TotalCustomersThisMonth = thisMonthCustomers,
            ActiveUsers = activeUsers,
            BookingsChangePercent = CalcChange(prevMonthBookings, thisMonthBookings),
            RevenueChangePercent = CalcChange((double)prevMonthRevenue, (double)thisMonthRevenue),
            CustomersChangePercent = CalcChange(prevMonthCustomers, thisMonthCustomers),
            ActiveUsersChangePercent = 0
        });
    }

    // GET /api/admin/dashboard/revenue?months=6
    [HttpGet("revenue")]
    public async Task<IActionResult> GetRevenue([FromQuery] int months = 6, CancellationToken ct = default)
    {
        if (months < 1) months = 6;
        if (months > 24) months = 24;

        var now = DateTime.UtcNow;
        var start = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-(months - 1));

        var bookings = await _db.Bookings
            .Where(b => b.CreatedAt >= start && (b.IsFinalized || SuccessStatuses.Contains(b.Status)))
            .Select(b => new { b.GrandTotal, b.CreatedAt })
            .ToListAsync(ct);

        var points = new List<DashboardRevenuePointDto>();
        for (int i = 0; i < months; i++)
        {
            var monthStart = start.AddMonths(i);
            var monthEnd = monthStart.AddMonths(1);
            var monthBookings = bookings.Where(b => b.CreatedAt >= monthStart && b.CreatedAt < monthEnd).ToList();

            points.Add(new DashboardRevenuePointDto
            {
                Month = monthStart.ToString("yyyy-MM"),
                Revenue = monthBookings.Sum(b => b.GrandTotal ?? 0),
                Bookings = monthBookings.Count
            });
        }

        return Ok(points);
    }

    // GET /api/admin/dashboard/recent-bookings?limit=10
    [HttpGet("recent-bookings")]
    public async Task<IActionResult> GetRecentBookings([FromQuery] int limit = 10, CancellationToken ct = default)
    {
        if (limit < 1) limit = 10;
        if (limit > 50) limit = 50;

        var bookings = await _db.Bookings
            .Include(b => b.Passengers)
            .OrderByDescending(b => b.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);

        var result = bookings.Select(b =>
        {
            var firstPax = b.Passengers.OrderBy(p => p.SequenceNo).FirstOrDefault();
            var cat = BookingStatusMapper.Categorize(b.Status, b.IsFinalized);
            return new DashboardRecentBookingDto
            {
                Id = b.Id.ToString(),
                PnrCode = b.PNR,
                PassengerName = firstPax != null ? $"{firstPax.FirstName} {firstPax.LastName}" : "—",
                Route = $"{b.Origin ?? "?"} → {b.Destination ?? "?"}",
                Amount = b.GrandTotal ?? 0,
                Currency = b.Currency ?? "TRY",
                Status = BookingStatusMapper.CategoryToString(cat),
                CreatedAt = b.CreatedAt.ToString("o")
            };
        }).ToList();

        return Ok(result);
    }

    private static double CalcChange(double prev, double current)
    {
        if (prev == 0) return current > 0 ? 100 : 0;
        return Math.Round((current - prev) / prev * 100, 1);
    }
}
