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
}