using System.Security.Claims;
using GBILET.Core.DTOs.Admin;
using GBILET.Core.Helpers;
using GBILET.Core.Interfaces;
using GBILET.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace GBILET.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/bookings")]
[Authorize]
[EnableRateLimiting("admin-general")]
public class AdminBookingsController : ControllerBase
{
    private readonly GTicketDbContext _db;
    private readonly IBookingSyncService _syncService;

    public AdminBookingsController(GTicketDbContext db, IBookingSyncService syncService)
    {
        _db = db;
        _syncService = syncService;
    }

    // GET /api/admin/bookings?page=1&pageSize=20&search=IST&status=confirmed&sortBy=createdAt&sortDir=desc
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] string sortBy = "createdAt",
        [FromQuery] string sortDir = "desc",
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var query = _db.Bookings.Include(b => b.Passengers).AsQueryable();

        // Status filter
        if (!string.IsNullOrEmpty(status) && status != "all")
        {
            if (Enum.TryParse<BookingStatusCategory>(status, true, out var cat))
            {
                var rawStatuses = BookingStatusMapper.GetRawStatusesForCategory(cat);
                if (cat == BookingStatusCategory.Confirmed)
                    query = query.Where(b => b.IsFinalized || rawStatuses.Contains(b.Status));
                else
                    query = query.Where(b => !b.IsFinalized && rawStatuses.Contains(b.Status));
            }
        }

        // Search filter
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search}%";
            query = query.Where(b =>
                (b.PNR != null && EF.Functions.ILike(b.PNR, term)) ||
                (b.InternalPnr != null && EF.Functions.ILike(b.InternalPnr, term)) ||
                (b.Origin != null && EF.Functions.ILike(b.Origin, term)) ||
                (b.Destination != null && EF.Functions.ILike(b.Destination, term)) ||
                b.Passengers.Any(p =>
                    EF.Functions.ILike(p.FirstName, term) ||
                    EF.Functions.ILike(p.LastName, term))
            );
        }

        var totalCount = await query.CountAsync(ct);

        // Sorting
        query = (sortBy?.ToLower(), sortDir?.ToLower()) switch
        {
            ("amount", "asc") => query.OrderBy(b => b.GrandTotal),
            ("amount", _) => query.OrderByDescending(b => b.GrandTotal),
            ("pnr", "asc") => query.OrderBy(b => b.PNR),
            ("pnr", _) => query.OrderByDescending(b => b.PNR),
            (_, "asc") => query.OrderBy(b => b.CreatedAt),
            _ => query.OrderByDescending(b => b.CreatedAt)
        };

        var bookings = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = bookings.Select(b =>
        {
            var firstPax = b.Passengers.OrderBy(p => p.SequenceNo).FirstOrDefault();
            var cat = BookingStatusMapper.Categorize(b.Status, b.IsFinalized);
            return new AdminBookingListItem
            {
                Id = b.Id,
                Pnr = b.PNR,
                InternalPnr = b.InternalPnr,
                PassengerName = firstPax != null ? $"{firstPax.FirstName} {firstPax.LastName}" : "—",
                PassengerCount = b.Passengers.Count,
                Route = $"{b.Origin ?? "?"} → {b.Destination ?? "?"}",
                AirlineCode = b.AirlineCode,
                FlightNumber = b.FlightNumber,
                Amount = b.GrandTotal ?? 0,
                Currency = b.Currency ?? "TRY",
                Status = b.Status,
                StatusCategory = BookingStatusMapper.CategoryToString(cat),
                IsFinalized = b.IsFinalized,
                CreatedAt = b.CreatedAt
            };
        }).ToList();

        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        return Ok(new AdminBookingListResponse
        {
            Data = items,
            Pagination = new PaginationInfo
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalPages
            }
        });
    }

    // GET /api/admin/bookings/{id}
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetDetail(Guid id, CancellationToken ct)
    {
        var booking = await _db.Bookings
            .Include(b => b.Passengers)
            .Include(b => b.FlightSegments)
            .Include(b => b.BookingLogs)
            .Include(b => b.ChangeLog)
            .Where(b => b.Id == id)
            .FirstOrDefaultAsync(ct);

        if (booking == null)
            return NotFound(new { error = "Rezervasyon bulunamadı." });

        var cat = BookingStatusMapper.Categorize(booking.Status, booking.IsFinalized);

        var detail = new AdminBookingDetail
        {
            Id = booking.Id,
            Pnr = booking.PNR,
            InternalPnr = booking.InternalPnr,
            Status = booking.Status,
            StatusCategory = BookingStatusMapper.CategoryToString(cat),
            IsFinalized = booking.IsFinalized,
            Amount = booking.GrandTotal ?? 0,
            Currency = booking.Currency ?? "TRY",
            ServiceFee = booking.ServiceFee,
            OurCommission = booking.OurCommission,
            Route = $"{booking.Origin ?? "?"} → {booking.Destination ?? "?"}",
            AirlineCode = booking.AirlineCode,
            FlightNumber = booking.FlightNumber,
            AdultCount = booking.AdultCount,
            ChildCount = booking.ChildCount,
            InfantCount = booking.InfantCount,
            CreatedAt = booking.CreatedAt,
            UpdatedAt = booking.UpdatedAt,
            AllocatedAt = booking.AllocatedAt,
            BookedAt = booking.BookedAt,
            PaidAt = booking.PaidAt,
            TicketedAt = booking.TicketedAt,
            CancelledAt = booking.CancelledAt,
            LastError = booking.LastError,
            Passengers = booking.Passengers
                .OrderBy(p => p.SequenceNo)
                .Select(p => new AdminPassengerDto
                {
                    Id = p.Id,
                    FirstName = p.FirstName,
                    LastName = p.LastName,
                    Email = p.Email,
                    Phone = p.Phone,
                    PassengerType = p.Type,
                    BirthDate = p.BirthDate,
                    Gender = p.Gender
                }).ToList(),
            Logs = booking.BookingLogs
                .OrderBy(l => l.CreatedAt)
                .Select(l => new AdminBookingLogDto
                {
                    Operation = l.Operation,
                    IsSuccess = l.IsSuccess,
                    ErrorMessage = l.ErrorMessage,
                    CreatedAt = l.CreatedAt,
                    ResponseTimeMs = l.ResponseTimeMs,
                    HttpStatusCode = l.HttpStatusCode
                }).ToList()
        };

        return Ok(detail);
    }

    // GET /api/admin/bookings/{id}/sync-preview
    [HttpGet("{id:guid}/sync-preview")]
    public async Task<IActionResult> SyncPreview(Guid id, CancellationToken ct)
    {
        try
        {
            var result = await _syncService.PreviewAsync(id, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // POST /api/admin/bookings/{id}/sync-confirm
    [HttpPost("{id:guid}/sync-confirm")]
    public async Task<IActionResult> SyncConfirm(Guid id, CancellationToken ct)
    {
        var adminId = GetAdminUserId();
        if (adminId == null)
            return Unauthorized(new { error = "Admin oturumu bulunamadı." });

        try
        {
            await _syncService.ConfirmAsync(id, adminId.Value, ct);
            return Ok(new { message = "Rezervasyon güncellendi, müşteriye email gönderildi." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private Guid? GetAdminUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? User.FindFirst("sub")?.Value
                    ?? User.FindFirst("adminUserId")?.Value;

        if (Guid.TryParse(claim, out var id))
            return id;
        return null;
    }
}
