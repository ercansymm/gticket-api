using GBILET.Core.DTOs.Support;

namespace GBILET.Core.Service.Support;

public interface ISupportTicketService
{
    // ============================================================
    // CUSTOMER (GBILET müşteri tarafı)
    // ============================================================

    Task<SupportTicketDetailDto> CreateByCustomerAsync(
        Guid userId,
        CreateSupportTicketRequest request,
        CancellationToken ct = default);

    Task<List<SupportTicketListItemDto>> GetByCustomerAsync(
        Guid userId,
        CancellationToken ct = default);

    Task<SupportTicketDetailDto?> GetDetailForCustomerAsync(
        Guid ticketId,
        Guid userId,
        CancellationToken ct = default);

    Task<SupportTicketMessageDto> AddMessageByCustomerAsync(
        Guid ticketId,
        Guid userId,
        AddSupportMessageRequest request,
        CancellationToken ct = default);

    // ============================================================
    // ADMIN (ATABİLET admin tarafı)
    // ============================================================

    Task<PagedResult<SupportTicketListItemDto>> GetAllForAdminAsync(
        AdminSupportTicketFilterRequest filter,
        CancellationToken ct = default);

    Task<SupportTicketDetailDto?> GetDetailForAdminAsync(
        Guid ticketId,
        CancellationToken ct = default);

    Task<SupportTicketMessageDto> AddMessageByAdminAsync(
        Guid ticketId,
        Guid adminUserId,
        AddSupportMessageRequest request,
        CancellationToken ct = default);

    Task<SupportTicketDetailDto> CloseByAdminAsync(
        Guid ticketId,
        Guid adminUserId,
        CancellationToken ct = default);

    // ============================================================
    // GUEST (üye olmayan müşteri — PNR + Soyad ile erişim)
    // ============================================================

    /// <summary>
    /// PNR + Soyad doğrulaması. Eşleşme varsa BookingId + yolcu ad-soyadı döner;
    /// yoksa null. Brute force ve enumerasyona karşı çağıran rate limit uygulamalıdır.
    /// </summary>
    Task<(Guid BookingId, string PassengerDisplayName)?> LookupGuestBookingAsync(
        string pnr,
        string surname,
        CancellationToken ct = default);

    Task<List<SupportTicketListItemDto>> GetByGuestBookingAsync(
        Guid bookingId,
        CancellationToken ct = default);

    Task<SupportTicketDetailDto> CreateByGuestAsync(
        Guid bookingId,
        string passengerDisplayName,
        GuestCreateSupportTicketRequest request,
        CancellationToken ct = default);

    Task<SupportTicketDetailDto?> GetDetailForGuestAsync(
        Guid ticketId,
        Guid bookingId,
        CancellationToken ct = default);

    Task<SupportTicketMessageDto> AddMessageByGuestAsync(
        Guid ticketId,
        Guid bookingId,
        string passengerDisplayName,
        AddSupportMessageRequest request,
        CancellationToken ct = default);
}