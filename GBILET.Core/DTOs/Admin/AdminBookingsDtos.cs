namespace GBILET.Core.DTOs.Admin;

// ===== Bookings List =====

public record AdminBookingListItem(
    Guid Id,
    string Pnr,
    string PassengerName,
    int PassengerCount,
    string Route,
    string? AirlineCode,
    string? FlightNumber,
    decimal? Amount,
    string Currency,
    string Status,
    string StatusCategory,
    bool IsFinalized,
    DateTime CreatedAt
);

public record AdminBookingListResponse(
    List<AdminBookingListItem> Data,
    PaginationInfo Pagination
);

public record PaginationInfo(
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages
);

// ===== Booking Detail =====

public record AdminBookingDetail(
    Guid Id,
    string Pnr,
    string? InternalPnr,
    string Status,
    string StatusCategory,
    bool IsFinalized,
    decimal? Amount,
    string Currency,
    decimal ServiceFee,
    decimal OurCommission,
    string Route,
    string? AirlineCode,
    string? FlightNumber,
    int AdultCount,
    int ChildCount,
    int InfantCount,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? AllocatedAt,
    DateTime? BookedAt,
    DateTime? PaidAt,
    DateTime? TicketedAt,
    DateTime? CancelledAt,
    string? LastError,
    List<AdminPassengerDto> Passengers,
    List<AdminBookingLogDto> Logs
);

public record AdminPassengerDto(
    Guid Id,
    string FirstName,
    string LastName,
    string? Email,
    string? Phone,
    string PassengerType,
    string? BirthDate,
    string? Gender
);

public record AdminBookingLogDto(
    string Operation,
    bool IsSuccess,
    string? ErrorMessage,
    DateTime CreatedAt,
    int? ResponseTimeMs,
    int? HttpStatusCode
);
