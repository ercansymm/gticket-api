using GBILET.Core.DTOs.Admin;
using GBILET.Core.Service.Admin;
using GBILET.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GBILET.Infrastructure.Services.Admin;

public class AdminCustomerService : IAdminCustomerService
{
    private readonly GTicketDbContext _db;

    public AdminCustomerService(GTicketDbContext db)
    {
        _db = db;
    }

    public async Task<CustomerListResponseDto> GetPassengersAsync(
        int page, int pageSize, string? search, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        // Project minimal fields, then group in-memory (EF GroupBy on ToLower is not translatable)
        var query = _db.Passengers
            .Include(p => p.Booking)
            .Where(p => p.Email != null && p.Email != "")
            .Select(p => new
            {
                p.FirstName,
                p.LastName,
                Email = p.Email!,
                p.Phone,
                p.BookingId,
                BookingCreatedAt = p.Booking.CreatedAt
            });

        // Apply search before materialization
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search}%";
            query = query.Where(p =>
                EF.Functions.ILike(p.FirstName, term) ||
                EF.Functions.ILike(p.LastName, term) ||
                EF.Functions.ILike(p.Email, term) ||
                (p.Phone != null && EF.Functions.ILike(p.Phone, term)));
        }

        var passengers = await query.ToListAsync(ct);

        var grouped = passengers
            .GroupBy(p => p.Email.ToLower())
            .Select(g =>
            {
                var mostRecent = g.OrderByDescending(p => p.BookingCreatedAt).First();
                return new CustomerListItemDto
                {
                    FullName = $"{mostRecent.FirstName} {mostRecent.LastName}",
                    Email = g.Key,
                    Phone = g.OrderByDescending(p => p.BookingCreatedAt)
                              .Select(p => p.Phone)
                              .FirstOrDefault(ph => !string.IsNullOrEmpty(ph)),
                    BookingCount = g.Select(p => p.BookingId).Distinct().Count()
                };
            })
            .OrderByDescending(c => c.BookingCount)
            .ThenBy(c => c.FullName)
            .ToList();

        var totalCount = grouped.Count;
        var items = grouped.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new CustomerListResponseDto
        {
            Items = items,
            Pagination = new PaginationInfo
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            }
        };
    }

    public async Task<CustomerListResponseDto> GetBookingContactsAsync(
        int page, int pageSize, string? search, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        // Booking contact = first passenger (SequenceNo == 1) of each booking.
        // ContactEmail/ContactPhone from the booking form is stored on the passenger with SequenceNo == 1.
        var query = _db.Passengers
            .Include(p => p.Booking)
            .Where(p => p.SequenceNo == 1 && p.Email != null && p.Email != "")
            .Select(p => new
            {
                p.FirstName,
                p.LastName,
                Email = p.Email!,
                p.Phone,
                p.BookingId,
                BookingCreatedAt = p.Booking.CreatedAt
            });

        // Apply search before materialization
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search}%";
            query = query.Where(p =>
                EF.Functions.ILike(p.FirstName, term) ||
                EF.Functions.ILike(p.LastName, term) ||
                EF.Functions.ILike(p.Email, term) ||
                (p.Phone != null && EF.Functions.ILike(p.Phone, term)));
        }

        var contacts = await query.ToListAsync(ct);

        var grouped = contacts
            .GroupBy(c => c.Email.ToLower())
            .Select(g =>
            {
                var mostRecent = g.OrderByDescending(c => c.BookingCreatedAt).First();
                return new CustomerListItemDto
                {
                    FullName = $"{mostRecent.FirstName} {mostRecent.LastName}",
                    Email = g.Key,
                    Phone = g.OrderByDescending(c => c.BookingCreatedAt)
                              .Select(c => c.Phone)
                              .FirstOrDefault(ph => !string.IsNullOrEmpty(ph)),
                    BookingCount = g.Select(c => c.BookingId).Distinct().Count()
                };
            })
            .OrderByDescending(c => c.BookingCount)
            .ThenBy(c => c.FullName)
            .ToList();

        var totalCount = grouped.Count;
        var items = grouped.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new CustomerListResponseDto
        {
            Items = items,
            Pagination = new PaginationInfo
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            }
        };
    }
}
