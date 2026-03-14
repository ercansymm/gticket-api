using GBILET.Core.Entities;
using GBILET.Core.Service;
using Microsoft.EntityFrameworkCore;

namespace GBILET.Infrastructure.Data;

public class BookingRepository : IBookingRepository
{
    private readonly GTicketDbContext _db;

    public BookingRepository(GTicketDbContext db)
    {
        _db = db;
    }

    public async Task<Booking> CreateBookingAsync(Booking booking)
    {
        _db.Bookings.Add(booking);
        await _db.SaveChangesAsync();
        return booking;
    }

    public async Task<Booking?> GetByIdAsync(Guid id)
    {
        return await _db.Bookings
            .Include(b => b.Passengers)
            .Include(b => b.FlightSegments)
            .Include(b => b.FareDetails)
            .Include(b => b.Payments)
            .Include(b => b.BillingInfo)
            .FirstOrDefaultAsync(b => b.Id == id);
    }

    public async Task<Booking?> GetByPnrAsync(string pnr)
    {
        return await _db.Bookings
            .Include(b => b.Passengers)
            .Include(b => b.FlightSegments)
            .FirstOrDefaultAsync(b => b.PNR == pnr);
    }

    public async Task UpdateStatusAsync(Guid bookingId, string status)
    {
        var booking = await _db.Bookings.FindAsync(bookingId);
        if (booking != null)
        {
            booking.Status = status;
            booking.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }

    public async Task AddLogAsync(BookingLog log)
    {
        _db.BookingLogs.Add(log);
        await _db.SaveChangesAsync();
    }
}