namespace GBILET.Core.DTOs.Admin;

// ===== Dashboard =====

public record DashboardStatsResponse(
    int TotalBookings,
    int TotalBookingsThisMonth,
    decimal TotalRevenue,
    decimal TotalRevenueThisMonth,
    int TotalCustomers,
    int TotalCustomersThisMonth,
    int ActiveUsers,
    decimal BookingsChangePercent,
    decimal RevenueChangePercent,
    decimal CustomersChangePercent,
    decimal ActiveUsersChangePercent
);

public record DashboardRevenuePoint(
    string Month,
    decimal Revenue,
    int Bookings
);

public record DashboardRecentBooking(
    Guid Id,
    string? PnrCode,
    string PassengerName,
    string Route,
    decimal Amount,
    string Currency,
    string Status,
    DateTime CreatedAt
);
