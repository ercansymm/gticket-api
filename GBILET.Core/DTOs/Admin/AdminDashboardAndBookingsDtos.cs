namespace GBILET.Core.DTOs.Admin;

// ===== Dashboard =====

public class DashboardStatsDto
{
    public int TotalBookings { get; set; }
    public int TotalBookingsThisMonth { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalRevenueThisMonth { get; set; }
    public int TotalCustomers { get; set; }
    public int TotalCustomersThisMonth { get; set; }
    public int ActiveUsers { get; set; }
    public double BookingsChangePercent { get; set; }
    public double RevenueChangePercent { get; set; }
    public double CustomersChangePercent { get; set; }
    public double ActiveUsersChangePercent { get; set; }
}

public class DashboardRevenuePointDto
{
    public string Month { get; set; } = "";
    public decimal Revenue { get; set; }
    public int Bookings { get; set; }
}

public class DashboardRecentBookingDto
{
    public string Id { get; set; } = "";
    public string? PnrCode { get; set; }
    public string PassengerName { get; set; } = "";
    public string Route { get; set; } = "";
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "TRY";
    public string Status { get; set; } = "";
    public string CreatedAt { get; set; } = "";
}

// ===== Bookings =====

public class AdminBookingListItem
{
    public Guid Id { get; set; }
    public string? Pnr { get; set; }
    public string? InternalPnr { get; set; }
    public string PassengerName { get; set; } = "";
    public int PassengerCount { get; set; }
    public string Route { get; set; } = "";
    public string? AirlineCode { get; set; }
    public string? FlightNumber { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "TRY";
    public string Status { get; set; } = "";
    public string StatusCategory { get; set; } = "";
    public bool IsFinalized { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PaginationInfo
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
}

public class AdminBookingListResponse
{
    public List<AdminBookingListItem> Data { get; set; } = new();
    public PaginationInfo Pagination { get; set; } = new();
}

public class AdminBookingDetail
{
    public Guid Id { get; set; }
    public string? Pnr { get; set; }
    public string? InternalPnr { get; set; }
    public string Status { get; set; } = "";
    public string StatusCategory { get; set; } = "";
    public bool IsFinalized { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "TRY";
    public decimal ServiceFee { get; set; }
    public decimal OurCommission { get; set; }
    public string Route { get; set; } = "";
    public string? AirlineCode { get; set; }
    public string? FlightNumber { get; set; }
    public int AdultCount { get; set; }
    public int ChildCount { get; set; }
    public int InfantCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? AllocatedAt { get; set; }
    public DateTime? BookedAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime? TicketedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? LastError { get; set; }
    public List<AdminPassengerDto> Passengers { get; set; } = new();
    public List<AdminBookingLogDto> Logs { get; set; } = new();
}

public class AdminPassengerDto
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string PassengerType { get; set; } = "";
    public string? BirthDate { get; set; }
    public string? Gender { get; set; }
}

public class AdminBookingLogDto
{
    public string Operation { get; set; } = "";
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? ResponseTimeMs { get; set; }
    public int? HttpStatusCode { get; set; }
}
