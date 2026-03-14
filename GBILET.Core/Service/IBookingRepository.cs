using GBILET.Core.Entities;

namespace GBILET.Core.Service;

public interface IBookingRepository
{
    Task<Booking> CreateBookingAsync(Booking booking);
    Task<Booking?> GetByIdAsync(Guid id);
    Task<Booking?> GetByPnrAsync(string pnr);
    Task UpdateStatusAsync(Guid bookingId, string status);
    Task AddLogAsync(BookingLog log);
}