using GBILET.Core.DTOs.Admin;
using GBILET.Core.Helpers;
using GBILET.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace GBILET.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/payments")]
[Authorize]
[EnableRateLimiting("admin-general")]
public class AdminPaymentsController : ControllerBase
{
    private readonly GTicketDbContext _db;

    public AdminPaymentsController(GTicketDbContext db)
    {
        _db = db;
    }

    // GET /api/admin/payments?page=1&pageSize=20&search=PNR&status=success
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] string? paymentType = null,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var query = _db.Payments
            .Include(p => p.Booking)
                .ThenInclude(b => b.Passengers)
            .AsQueryable();

        // Status filter (frontend kategorisi)
        if (!string.IsNullOrWhiteSpace(status) && status != "all")
        {
            var s = status.ToLowerInvariant();
            query = s switch
            {
                "success" => query.Where(p => p.Status == "Success"),
                "failed" => query.Where(p => p.Status == "Failed"),
                "pending" => query.Where(p => p.Status == "Pending3D" || p.Status == "Pending"),
                _ => query
            };
        }

        if (!string.IsNullOrWhiteSpace(paymentType))
        {
            query = query.Where(p => p.PaymentType == paymentType);
        }

        // Search: PNR / InternalPnr / yolcu adı
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search}%";
            query = query.Where(p =>
                (p.Booking.PNR != null && EF.Functions.ILike(p.Booking.PNR, term)) ||
                (p.Booking.InternalPnr != null && EF.Functions.ILike(p.Booking.InternalPnr, term)) ||
                p.Booking.Passengers.Any(x =>
                    EF.Functions.ILike(x.FirstName, term) ||
                    EF.Functions.ILike(x.LastName, term))
            );
        }

        var totalCount = await query.CountAsync(ct);

        var payments = await query
            .OrderByDescending(p => p.TransactionDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = payments.Select(MapListItem).ToList();
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        return Ok(new AdminPaymentListResponse
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

    // GET /api/admin/payments/{id}
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetDetail(Guid id, CancellationToken ct)
    {
        var payment = await _db.Payments
            .Include(p => p.Booking)
                .ThenInclude(b => b.Passengers)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (payment == null)
            return NotFound(new { error = "Ödeme bulunamadı." });

        var booking = payment.Booking;
        var firstPax = booking.Passengers.OrderBy(x => x.SequenceNo).FirstOrDefault();
        var bookingCat = BookingStatusMapper.Categorize(booking.Status, booking.IsFinalized);

        var detail = new AdminPaymentDetail
        {
            Id = payment.Id,
            BookingId = payment.BookingId,
            Pnr = booking.PNR,
            InternalPnr = booking.InternalPnr,
            Route = $"{booking.Origin ?? "?"} → {booking.Destination ?? "?"}",
            AirlineCode = booking.AirlineCode,
            FlightNumber = booking.FlightNumber,
            BookingStatus = booking.Status,
            BookingStatusCategory = BookingStatusMapper.CategoryToString(bookingCat),

            CustomerName = firstPax != null ? $"{firstPax.FirstName} {firstPax.LastName}" : "—",
            CustomerEmail = firstPax?.Email,
            CustomerPhone = firstPax?.Phone,

            Amount = payment.Amount,
            Currency = payment.Currency,
            PaymentType = payment.PaymentType,
            MaskedCardNumber = payment.MaskedCardNumber,
            CardHolder = payment.CardHolder ?? payment.CardHolderName,
            InstallmentCount = payment.InstallmentCount,
            Is3DSecure = payment.Is3DSecure,

            Status = payment.Status,
            StatusCategory = PaymentStatusMapper.CategoryOf(payment.Status),

            ErrorCode = payment.ErrorCode,
            ErrorCodeLabel = payment.ErrorCode != null
                ? PaymentStatusMapper.LabelForErrorCode(payment.ErrorCode)
                : null,
            ErrorMessage = payment.ErrorMessage,

            BiletBankPaymentId = payment.BiletBankPaymentId,
            ProviderTransactionId = payment.ProviderTransactionId,

            TransactionDate = payment.TransactionDate,
            RefundedAt = payment.RefundedAt,
            RefundAmount = payment.RefundAmount
        };

        return Ok(detail);
    }

    // GET /api/admin/payments/failed-3d?page=1&pageSize=20
    [HttpGet("failed-3d")]
    public async Task<IActionResult> Failed3D(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? errorCode = null,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        // 3D başarısız: Status=Failed VE Is3DSecure=true (veya PaymentType==CreditCard ve hata var)
        var baseQuery = _db.Payments
            .Include(p => p.Booking)
                .ThenInclude(b => b.Passengers)
            .Where(p => p.Status == "Failed" && p.Is3DSecure);

        // Hata kategorisi grupları (filtre uygulanmadan)
        var groupRows = await baseQuery
            .GroupBy(p => p.ErrorCode ?? "Other")
            .Select(g => new { Code = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var groups = groupRows
            .Select(g => new AdminFailed3DGroup
            {
                ErrorCode = g.Code,
                Label = PaymentStatusMapper.LabelForErrorCode(g.Code),
                Count = g.Count
            })
            .OrderByDescending(g => g.Count)
            .ToList();

        // Liste (errorCode filtresi varsa uygula)
        var listQuery = baseQuery;
        if (!string.IsNullOrWhiteSpace(errorCode))
        {
            listQuery = listQuery.Where(p => (p.ErrorCode ?? "Other") == errorCode);
        }

        var totalCount = await listQuery.CountAsync(ct);

        var payments = await listQuery
            .OrderByDescending(p => p.TransactionDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = payments.Select(MapListItem).ToList();
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        return Ok(new AdminFailed3DResponse
        {
            Groups = groups,
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

    // ── helpers ──
    private static AdminPaymentListItem MapListItem(Core.Entities.Payment p)
    {
        var booking = p.Booking;
        var firstPax = booking.Passengers.OrderBy(x => x.SequenceNo).FirstOrDefault();

        return new AdminPaymentListItem
        {
            Id = p.Id,
            BookingId = p.BookingId,
            Pnr = booking.PNR,
            InternalPnr = booking.InternalPnr,
            Route = $"{booking.Origin ?? "?"} → {booking.Destination ?? "?"}",
            CustomerName = firstPax != null ? $"{firstPax.FirstName} {firstPax.LastName}" : "—",
            Amount = p.Amount,
            Currency = p.Currency,
            PaymentType = p.PaymentType,
            MaskedCardNumber = p.MaskedCardNumber,
            InstallmentCount = p.InstallmentCount,
            Status = p.Status,
            StatusCategory = PaymentStatusMapper.CategoryOf(p.Status),
            Is3DSecure = p.Is3DSecure,
            ErrorCode = p.ErrorCode,
            ErrorMessage = p.ErrorMessage,
            TransactionDate = p.TransactionDate
        };
    }
}
