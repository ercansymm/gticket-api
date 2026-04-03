using GBILET.Core.Entities;

namespace GBILET.Core.Service;

public interface IBookingRepository
{
    Task<Booking> CreateBookingAsync(Booking booking);
    Task<Booking?> GetByIdAsync(Guid id);
    Task<Booking?> GetByPnrAsync(string pnr);
    Task UpdateStatusAsync(Guid bookingId, string status);
    Task UpdatePnrAsync(Guid bookingId, string pnr);
    Task AddLogAsync(BookingLog log);

    // Admin takibi
    Task<List<Booking>> GetByUserIdAsync(Guid userId);
    Task<List<Booking>> GetByGuestSessionIdAsync(Guid guestSessionId);
    Task<GuestSession> CreateGuestSessionAsync(GuestSession guestSession);
    Task<GuestSession?> GetGuestSessionByEmailAsync(string email);
}
