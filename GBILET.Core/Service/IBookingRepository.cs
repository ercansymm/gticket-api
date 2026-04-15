using GBILET.Core.Entities;

namespace GBILET.Core.Service;

public interface IBookingRepository
{
    Task<Booking> CreateBookingAsync(Booking booking);
    Task<Booking?> GetByIdAsync(Guid id);
    Task<Booking?> GetByPnrAsync(string pnr);
    Task<Booking?> GetByInternalPnrAndLastNameAsync(string internalPnr, string lastName);
    Task<Booking?> GetByShoppingFileIdAsync(string shoppingFileId);
    Task UpdateStatusAsync(Guid bookingId, string status);
    Task UpdatePnrAsync(Guid bookingId, string pnr);
    Task AddLogAsync(BookingLog log);
    
    Task<bool> InternalPnrExistsAsync(string internalPnr);
    Task UpdateInternalPnrAsync(Guid bookingId, string internalPnr);

    // Admin takibi

    Task<List<Booking>> GetByUserIdAsync(Guid userId);
    Task<List<Booking>> GetByGuestSessionIdAsync(Guid guestSessionId);
    Task<GuestSession> CreateGuestSessionAsync(GuestSession guestSession);
    Task<GuestSession?> GetGuestSessionByEmailAsync(string email);
}
