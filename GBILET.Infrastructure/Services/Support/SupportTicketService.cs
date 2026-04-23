using GBILET.Core.DTOs.Support;
using GBILET.Core.Entities.Support;
using GBILET.Core.Service.Support;
using GBILET.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GBILET.Infrastructure.Services.Support;

public class SupportTicketService : ISupportTicketService
{
    private readonly GTicketDbContext _db;
    private readonly ILogger<SupportTicketService> _logger;

    public SupportTicketService(
        GTicketDbContext db,
        ILogger<SupportTicketService> logger)
    {
        _db = db;
        _logger = logger;
    }

    // ============================================================
    // CUSTOMER: CREATE
    // ============================================================
    public async Task<SupportTicketDetailDto> CreateByCustomerAsync(
        Guid userId,
        CreateSupportTicketRequest request,
        CancellationToken ct = default)
    {
        // Validasyon: Subject ve Message
        if (string.IsNullOrWhiteSpace(request.Subject))
            throw new InvalidOperationException("Konu boş olamaz.");
        if (string.IsNullOrWhiteSpace(request.Message))
            throw new InvalidOperationException("Mesaj boş olamaz.");
        if (request.Subject.Length > 200)
            throw new InvalidOperationException("Konu en fazla 200 karakter olabilir.");

        // Validasyon: BookingId zorunluluğu (Refund/Change için)
        var requiresBooking = request.Type is SupportTicketType.Refund or SupportTicketType.Change;
        if (requiresBooking && !request.BookingId.HasValue)
        {
            throw new InvalidOperationException(
                "İade ve değişiklik talepleri için rezervasyon seçimi zorunludur.");
        }

        // User var mı?
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new KeyNotFoundException("Kullanıcı bulunamadı.");

        // BookingId varsa: müşteriye ait olduğunu doğrula
        if (request.BookingId.HasValue)
        {
            var bookingOwned = await _db.Bookings
                .AnyAsync(b => b.Id == request.BookingId.Value && b.UserId == userId, ct);

            if (!bookingOwned)
                throw new InvalidOperationException("Bu rezervasyon size ait değil veya bulunamadı.");
        }

        var now = DateTime.UtcNow;
        var ticketId = Guid.NewGuid();
        var ticketNumber = await GenerateTicketNumberAsync(ct);

        var ticket = new SupportTicket
        {
            Id = ticketId,
            TicketNumber = ticketNumber,
            UserId = userId,
            BookingId = request.BookingId,
            Type = request.Type,
            Subject = request.Subject.Trim(),
            Status = SupportTicketStatus.Open,
            CreatedAt = now,
            UpdatedAt = now,
            LastActivityAt = now
        };

        var firstMessage = new SupportTicketMessage
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            SenderType = SupportMessageSenderType.Customer,
            SenderId = userId,
            SenderDisplayName = user.FullName,
            Body = request.Message.Trim(),
            CreatedAt = now
        };

        _db.SupportTickets.Add(ticket);
        _db.SupportTicketMessages.Add(firstMessage);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Support ticket {TicketNumber} created by user {UserId} (type={Type})",
            ticketNumber, userId, request.Type);

        return (await LoadDetailAsync(ticketId, ct))!;
    }

    // ============================================================
    // CUSTOMER: LIST
    // ============================================================
    public async Task<List<SupportTicketListItemDto>> GetByCustomerAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        return await _db.SupportTickets
            .AsNoTracking()
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.LastActivityAt)
            .Select(t => new SupportTicketListItemDto
            {
                Id = t.Id,
                TicketNumber = t.TicketNumber,
                Type = t.Type,
                Subject = t.Subject,
                Status = t.Status,
                BookingId = t.BookingId,
                BookingPnr = t.Booking != null ? t.Booking.PNR : null,
                UserId = t.UserId,
                GuestSessionId = t.GuestSessionId,
                UserFullName = t.User != null ? t.User.FullName : null,
                UserEmail = t.User != null ? t.User.Email : null,
                MessageCount = t.Messages.Count,
                LastMessagePreview = t.Messages
                    .OrderByDescending(m => m.CreatedAt)
                    .Select(m => m.Body)
                    .FirstOrDefault(),
                LastMessageSenderType = t.Messages
                    .OrderByDescending(m => m.CreatedAt)
                    .Select(m => (SupportMessageSenderType?)m.SenderType)
                    .FirstOrDefault(),
                LastActivityAt = t.LastActivityAt,
                CreatedAt = t.CreatedAt
            })
            .ToListAsync(ct);
    }

    // ============================================================
    // CUSTOMER: DETAIL
    // ============================================================
    public async Task<SupportTicketDetailDto?> GetDetailForCustomerAsync(
        Guid ticketId,
        Guid userId,
        CancellationToken ct = default)
    {
        // Sahiplik kontrolü
        var isOwner = await _db.SupportTickets
            .AnyAsync(t => t.Id == ticketId && t.UserId == userId, ct);

        if (!isOwner)
            return null;

        return await LoadDetailAsync(ticketId, ct);
    }

    // ============================================================
    // CUSTOMER: ADD MESSAGE
    // ============================================================
    public async Task<SupportTicketMessageDto> AddMessageByCustomerAsync(
        Guid ticketId,
        Guid userId,
        AddSupportMessageRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Body))
            throw new InvalidOperationException("Mesaj boş olamaz.");

        var ticket = await _db.SupportTickets
            .FirstOrDefaultAsync(t => t.Id == ticketId && t.UserId == userId, ct)
            ?? throw new KeyNotFoundException("Destek talebi bulunamadı.");

        if (ticket.Status == SupportTicketStatus.Closed)
            throw new InvalidOperationException("Kapalı bir talebe mesaj ekleyemezsiniz.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new KeyNotFoundException("Kullanıcı bulunamadı.");

        var now = DateTime.UtcNow;
        var message = new SupportTicketMessage
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            SenderType = SupportMessageSenderType.Customer,
            SenderId = userId,
            SenderDisplayName = user.FullName,
            Body = request.Body.Trim(),
            CreatedAt = now
        };

        ticket.LastActivityAt = now;
        ticket.UpdatedAt = now;

        _db.SupportTicketMessages.Add(message);
        await _db.SaveChangesAsync(ct);

        return ToMessageDto(message);
    }

    // ============================================================
    // ADMIN: LIST
    // ============================================================
    public async Task<PagedResult<SupportTicketListItemDto>> GetAllForAdminAsync(
        AdminSupportTicketFilterRequest filter,
        CancellationToken ct = default)
    {
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize is < 1 or > 100 ? 20 : filter.PageSize;

        var query = _db.SupportTickets.AsNoTracking().AsQueryable();

        if (filter.Status.HasValue)
            query = query.Where(t => t.Status == filter.Status.Value);

        if (filter.Type.HasValue)
            query = query.Where(t => t.Type == filter.Type.Value);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.Trim().ToLower();
            query = query.Where(t =>
                t.TicketNumber.ToLower().Contains(s) ||
                t.Subject.ToLower().Contains(s) ||
                (t.Booking != null && t.Booking.PNR != null && t.Booking.PNR.ToLower().Contains(s)) ||
                (t.User != null && t.User.FullName.ToLower().Contains(s)) ||
                (t.User != null && t.User.Email.ToLower().Contains(s)));
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(t => t.LastActivityAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new SupportTicketListItemDto
            {
                Id = t.Id,
                TicketNumber = t.TicketNumber,
                Type = t.Type,
                Subject = t.Subject,
                Status = t.Status,
                BookingId = t.BookingId,
                BookingPnr = t.Booking != null ? t.Booking.PNR : null,
                UserId = t.UserId,
                GuestSessionId = t.GuestSessionId,
                UserFullName = t.User != null ? t.User.FullName : null,
                UserEmail = t.User != null ? t.User.Email : null,
                MessageCount = t.Messages.Count,
                LastMessagePreview = t.Messages
                    .OrderByDescending(m => m.CreatedAt)
                    .Select(m => m.Body)
                    .FirstOrDefault(),
                LastMessageSenderType = t.Messages
                    .OrderByDescending(m => m.CreatedAt)
                    .Select(m => (SupportMessageSenderType?)m.SenderType)
                    .FirstOrDefault(),
                LastActivityAt = t.LastActivityAt,
                CreatedAt = t.CreatedAt
            })
            .ToListAsync(ct);

        return new PagedResult<SupportTicketListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    // ============================================================
    // ADMIN: DETAIL
    // ============================================================
    public async Task<SupportTicketDetailDto?> GetDetailForAdminAsync(
        Guid ticketId,
        CancellationToken ct = default)
    {
        return await LoadDetailAsync(ticketId, ct);
    }

    // ============================================================
    // ADMIN: ADD MESSAGE
    // ============================================================
    public async Task<SupportTicketMessageDto> AddMessageByAdminAsync(
        Guid ticketId,
        Guid adminUserId,
        AddSupportMessageRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Body))
            throw new InvalidOperationException("Mesaj boş olamaz.");

        var ticket = await _db.SupportTickets.FirstOrDefaultAsync(t => t.Id == ticketId, ct)
            ?? throw new KeyNotFoundException("Destek talebi bulunamadı.");

        if (ticket.Status == SupportTicketStatus.Closed)
            throw new InvalidOperationException("Kapalı bir talebe mesaj ekleyemezsiniz.");

        var admin = await _db.AdminUsers.FirstOrDefaultAsync(a => a.Id == adminUserId, ct)
            ?? throw new KeyNotFoundException("Admin kullanıcı bulunamadı.");

        var now = DateTime.UtcNow;
        var message = new SupportTicketMessage
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            SenderType = SupportMessageSenderType.Admin,
            SenderId = adminUserId,
            SenderDisplayName = admin.FullName,
            Body = request.Body.Trim(),
            CreatedAt = now
        };

        ticket.LastActivityAt = now;
        ticket.UpdatedAt = now;

        _db.SupportTicketMessages.Add(message);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Admin {AdminId} replied to support ticket {TicketNumber}",
            adminUserId, ticket.TicketNumber);

        return ToMessageDto(message);
    }

    // ============================================================
    // ADMIN: CLOSE
    // ============================================================
    public async Task<SupportTicketDetailDto> CloseByAdminAsync(
        Guid ticketId,
        Guid adminUserId,
        CancellationToken ct = default)
    {
        var ticket = await _db.SupportTickets.FirstOrDefaultAsync(t => t.Id == ticketId, ct)
            ?? throw new KeyNotFoundException("Destek talebi bulunamadı.");

        if (ticket.Status == SupportTicketStatus.Closed)
        {
            // Zaten kapalı — idempotent dön
            return (await LoadDetailAsync(ticketId, ct))!;
        }

        var now = DateTime.UtcNow;
        ticket.Status = SupportTicketStatus.Closed;
        ticket.ClosedAt = now;
        ticket.ClosedByAdminId = adminUserId;
        ticket.UpdatedAt = now;
        ticket.LastActivityAt = now;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Admin {AdminId} closed support ticket {TicketNumber}",
            adminUserId, ticket.TicketNumber);

        return (await LoadDetailAsync(ticketId, ct))!;
    }

    // ============================================================
    // GUEST: LOOKUP (PNR + Soyad)
    // ============================================================
    public async Task<(Guid BookingId, string PassengerDisplayName)?> LookupGuestBookingAsync(
        string pnr,
        string surname,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(pnr) || string.IsNullOrWhiteSpace(surname))
            return null;

        var pnrTrim = pnr.Trim().ToUpperInvariant();
        var surnameTrim = surname.Trim();

        // PNR'a göre booking bul
        var booking = await _db.Bookings
            .AsNoTracking()
            .Include(b => b.Passengers)
            .Where(b => b.PNR != null && b.PNR.ToUpper() == pnrTrim)
            .FirstOrDefaultAsync(ct);

        if (booking == null)
        {
            _logger.LogWarning("Guest lookup: PNR {Pnr} ile eşleşen Booking bulunamadı.", pnrTrim);
            return null;
        }

        // Yolculardan biri girilen soyadla eşleşmeli.
        // Türkçe karakterler (İ/ı, Ş/ş, Ğ/ğ, Ü/ü, Ö/ö, Ç/ç) normalize edilerek karşılaştırılır;
        // OrdinalIgnoreCase Türkçe karakter eşleşmelerini garanti etmez.
        var surnameNorm = NormalizeForCompare(surnameTrim);

        var matched = booking.Passengers
            .FirstOrDefault(p =>
                !string.IsNullOrWhiteSpace(p.LastName) &&
                NormalizeForCompare(p.LastName) == surnameNorm);

        if (matched == null)
        {
            _logger.LogWarning(
                "Guest lookup: PNR {Pnr} bulundu (BookingId={BookingId}) ama soyad eşleşmedi. Girilen='{Surname}' Yolcular=[{Names}]",
                pnrTrim, booking.Id, surnameTrim,
                string.Join(", ", booking.Passengers.Select(p => p.LastName)));
            return null;
        }

        var displayName = $"{matched.FirstName} {matched.LastName}".Trim();
        return (booking.Id, displayName);
    }

    // Türkçe duyarlı normalize: trim + Türkçe harfleri ASCII karşılıklarına çevirir + upper-invariant
    private static string NormalizeForCompare(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var s = value.Trim();
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (var ch in s)
        {
            switch (ch)
            {
                case 'İ': case 'I': case 'ı': case 'i': sb.Append('I'); break;
                case 'Ş': case 'ş': sb.Append('S'); break;
                case 'Ğ': case 'ğ': sb.Append('G'); break;
                case 'Ü': case 'ü': sb.Append('U'); break;
                case 'Ö': case 'ö': sb.Append('O'); break;
                case 'Ç': case 'ç': sb.Append('C'); break;
                default: sb.Append(char.ToUpperInvariant(ch)); break;
            }
        }
        return sb.ToString();
    }

    // ============================================================
    // GUEST: LIST (BookingId scope)
    // ============================================================
    public async Task<List<SupportTicketListItemDto>> GetByGuestBookingAsync(
        Guid bookingId,
        CancellationToken ct = default)
    {
        return await _db.SupportTickets
            .AsNoTracking()
            .Where(t => t.BookingId == bookingId)
            .OrderByDescending(t => t.LastActivityAt)
            .Select(t => new SupportTicketListItemDto
            {
                Id = t.Id,
                TicketNumber = t.TicketNumber,
                Type = t.Type,
                Subject = t.Subject,
                Status = t.Status,
                BookingId = t.BookingId,
                BookingPnr = t.Booking != null ? t.Booking.PNR : null,
                UserId = t.UserId,
                GuestSessionId = t.GuestSessionId,
                UserFullName = t.User != null ? t.User.FullName : null,
                UserEmail = t.User != null ? t.User.Email : null,
                MessageCount = t.Messages.Count,
                LastMessagePreview = t.Messages
                    .OrderByDescending(m => m.CreatedAt)
                    .Select(m => m.Body)
                    .FirstOrDefault(),
                LastMessageSenderType = t.Messages
                    .OrderByDescending(m => m.CreatedAt)
                    .Select(m => (SupportMessageSenderType?)m.SenderType)
                    .FirstOrDefault(),
                LastActivityAt = t.LastActivityAt,
                CreatedAt = t.CreatedAt
            })
            .ToListAsync(ct);
    }

    // ============================================================
    // GUEST: CREATE
    // ============================================================
    public async Task<SupportTicketDetailDto> CreateByGuestAsync(
        Guid bookingId,
        string passengerDisplayName,
        GuestCreateSupportTicketRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Subject))
            throw new InvalidOperationException("Konu boş olamaz.");
        if (string.IsNullOrWhiteSpace(request.Message))
            throw new InvalidOperationException("Mesaj boş olamaz.");
        if (request.Subject.Length > 200)
            throw new InvalidOperationException("Konu en fazla 200 karakter olabilir.");

        var booking = await _db.Bookings
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == bookingId, ct)
            ?? throw new KeyNotFoundException("Rezervasyon bulunamadı.");

        var now = DateTime.UtcNow;
        var ticketId = Guid.NewGuid();
        var ticketNumber = await GenerateTicketNumberAsync(ct);

        var ticket = new SupportTicket
        {
            Id = ticketId,
            TicketNumber = ticketNumber,
            UserId = booking.UserId,                  // Booking üyenin ise yine User'a bağlanır
            GuestSessionId = booking.GuestSessionId,  // Misafir booking ise GuestSession'a bağlanır
            BookingId = bookingId,
            Type = request.Type,
            Subject = request.Subject.Trim(),
            Status = SupportTicketStatus.Open,
            CreatedAt = now,
            UpdatedAt = now,
            LastActivityAt = now
        };

        var firstMessage = new SupportTicketMessage
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            SenderType = SupportMessageSenderType.Customer,
            SenderId = booking.UserId,                // Misafir ise null
            SenderDisplayName = string.IsNullOrWhiteSpace(passengerDisplayName)
                ? "Misafir"
                : passengerDisplayName,
            Body = request.Message.Trim(),
            CreatedAt = now
        };

        _db.SupportTickets.Add(ticket);
        _db.SupportTicketMessages.Add(firstMessage);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Guest support ticket {TicketNumber} created for booking {BookingId} (type={Type})",
            ticketNumber, bookingId, request.Type);

        return (await LoadDetailAsync(ticketId, ct))!;
    }

    // ============================================================
    // GUEST: DETAIL (BookingId scope)
    // ============================================================
    public async Task<SupportTicketDetailDto?> GetDetailForGuestAsync(
        Guid ticketId,
        Guid bookingId,
        CancellationToken ct = default)
    {
        var matches = await _db.SupportTickets
            .AnyAsync(t => t.Id == ticketId && t.BookingId == bookingId, ct);

        if (!matches)
            return null;

        return await LoadDetailAsync(ticketId, ct);
    }

    // ============================================================
    // GUEST: ADD MESSAGE
    // ============================================================
    public async Task<SupportTicketMessageDto> AddMessageByGuestAsync(
        Guid ticketId,
        Guid bookingId,
        string passengerDisplayName,
        AddSupportMessageRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Body))
            throw new InvalidOperationException("Mesaj boş olamaz.");

        var ticket = await _db.SupportTickets
            .FirstOrDefaultAsync(t => t.Id == ticketId && t.BookingId == bookingId, ct)
            ?? throw new KeyNotFoundException("Destek talebi bulunamadı.");

        if (ticket.Status == SupportTicketStatus.Closed)
            throw new InvalidOperationException("Kapalı bir talebe mesaj ekleyemezsiniz.");

        var now = DateTime.UtcNow;
        var message = new SupportTicketMessage
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            SenderType = SupportMessageSenderType.Customer,
            SenderId = ticket.UserId,
            SenderDisplayName = string.IsNullOrWhiteSpace(passengerDisplayName)
                ? "Misafir"
                : passengerDisplayName,
            Body = request.Body.Trim(),
            CreatedAt = now
        };

        ticket.LastActivityAt = now;
        ticket.UpdatedAt = now;

        _db.SupportTicketMessages.Add(message);
        await _db.SaveChangesAsync(ct);

        return ToMessageDto(message);
    }

    // ============================================================
    // Helpers
    // ============================================================

    private async Task<SupportTicketDetailDto?> LoadDetailAsync(Guid ticketId, CancellationToken ct)
    {
        var ticket = await _db.SupportTickets
            .AsNoTracking()
            .Include(t => t.User)
            .Include(t => t.Booking)
            .Include(t => t.ClosedByAdmin)
            .Include(t => t.Messages)
            .FirstOrDefaultAsync(t => t.Id == ticketId, ct);

        if (ticket == null)
            return null;

        return new SupportTicketDetailDto
        {
            Id = ticket.Id,
            TicketNumber = ticket.TicketNumber,
            Type = ticket.Type,
            Subject = ticket.Subject,
            Status = ticket.Status,
            BookingId = ticket.BookingId,
            BookingPnr = ticket.Booking?.PNR,
            BookingOrigin = ticket.Booking?.Origin,
            BookingDestination = ticket.Booking?.Destination,
            BookingStatus = ticket.Booking?.Status,
            UserId = ticket.UserId,
            GuestSessionId = ticket.GuestSessionId,
            UserFullName = ticket.User?.FullName,
            UserEmail = ticket.User?.Email,
            UserPhone = ticket.User?.Phone,
            ClosedAt = ticket.ClosedAt,
            ClosedByAdminId = ticket.ClosedByAdminId,
            ClosedByAdminName = ticket.ClosedByAdmin?.FullName,
            CreatedAt = ticket.CreatedAt,
            UpdatedAt = ticket.UpdatedAt,
            LastActivityAt = ticket.LastActivityAt,
            Messages = ticket.Messages
                .OrderBy(m => m.CreatedAt)
                .Select(ToMessageDto)
                .ToList()
        };
    }

    private static SupportTicketMessageDto ToMessageDto(SupportTicketMessage m) => new()
    {
        Id = m.Id,
        SenderType = m.SenderType,
        SenderId = m.SenderId,
        SenderDisplayName = m.SenderDisplayName,
        Body = m.Body,
        CreatedAt = m.CreatedAt
    };

    /// <summary>
    /// Ticket numarası üretir. Format: TKT-{YIL}-{5 haneli sıra no}
    /// Örn: TKT-2026-00001
    /// </summary>
    private async Task<string> GenerateTicketNumberAsync(CancellationToken ct)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"TKT-{year}-";

        // Aynı yıl içinde oluşturulmuş ticket sayısına göre sıralı numara üret
        var countThisYear = await _db.SupportTickets
            .CountAsync(t => t.TicketNumber.StartsWith(prefix), ct);

        // Çakışma ihtimaline karşı 10 defa dene
        for (int attempt = 0; attempt < 10; attempt++)
        {
            var candidate = $"{prefix}{(countThisYear + 1 + attempt):D5}";
            var exists = await _db.SupportTickets
                .AnyAsync(t => t.TicketNumber == candidate, ct);
            if (!exists)
                return candidate;
        }

        // Fallback: Guid'den türet (asla ulaşılmamalı)
        return $"{prefix}{Guid.NewGuid().ToString()[..8].ToUpper()}";
    }
}