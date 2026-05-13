using GBILET.Core.Entities;
using GBILET.Core.Interfaces;
using GBILET.Core.Service;
using GBILET.Core.Service.Email;
using GBILET.Core.Service.Flight;
using GBILET.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GBILET.Infrastructure.Services;

public class BookingSyncService : IBookingSyncService
{
    private readonly GTicketDbContext _db;
    private readonly IFlightService _flightService;
    private readonly IEmailService _emailService;
    private readonly ILogger<BookingSyncService> _logger;

    public BookingSyncService(
        GTicketDbContext db,
        IFlightService flightService,
        IEmailService emailService,
        ILogger<BookingSyncService> logger)
    {
        _db = db;
        _flightService = flightService;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<SyncPreviewResult> PreviewAsync(Guid bookingId, CancellationToken ct = default)
    {
        var booking = await _db.Bookings
            .Include(b => b.FlightSegments)
            .Include(b => b.Passengers)
            .FirstOrDefaultAsync(b => b.Id == bookingId, ct)
            ?? throw new InvalidOperationException("Rezervasyon bulunamadı.");

        if (string.IsNullOrWhiteSpace(booking.ShoppingFileId))
            throw new InvalidOperationException("Bu rezervasyonun BiletBank ShoppingFileId bilgisi eksik.");

        var biletBank = await _flightService.ReadShoppingFileWithAutoLoginAsync(booking.ShoppingFileId, ct);

        if (biletBank.HasError)
            throw new InvalidOperationException($"BiletBank sorgusu başarısız: {biletBank.ErrorMessage}");

        var changes = new List<BookingFieldChange>();

        // İptal durumu kontrolü
        if (biletBank.IsReservationCancelled && booking.Status != "Cancelled")
        {
            changes.Add(new BookingFieldChange("Durum", "Aktif", "İptal"));
        }

        // Segment karşılaştırması (sıra bazlı)
        var dbSegments = booking.FlightSegments.OrderBy(s => s.SequenceNo).ToList();
        var bbSegments = biletBank.Segments;

        for (var i = 0; i < Math.Min(dbSegments.Count, bbSegments.Count); i++)
        {
            var db = dbSegments[i];
            var bb = bbSegments[i];

            if (DateTime.TryParse(bb.DepartureDay, out var bbDate))
            {
                var dbDate = db.DepartureDate.Date;
                if (dbDate != bbDate.Date)
                    changes.Add(new BookingFieldChange(
                        $"Kalkış Tarihi (Segment {i + 1})",
                        dbDate.ToString("dd.MM.yyyy"),
                        bbDate.ToString("dd.MM.yyyy")));
            }

            if (!string.IsNullOrWhiteSpace(bb.FlightNumber) && bb.FlightNumber != db.FlightNumber)
                changes.Add(new BookingFieldChange(
                    $"Uçuş Numarası (Segment {i + 1})",
                    db.FlightNumber ?? "-",
                    bb.FlightNumber));

            if (!string.IsNullOrWhiteSpace(bb.DepartureTime) && bb.DepartureTime != db.DepartureTime)
                changes.Add(new BookingFieldChange(
                    $"Kalkış Saati (Segment {i + 1})",
                    db.DepartureTime ?? "-",
                    bb.DepartureTime));
        }

        // Bilet numarası karşılaştırması
        var dbPassengers = booking.Passengers.OrderBy(p => p.SequenceNo).ToList();
        var bbPassengers = biletBank.Passengers;

        for (var i = 0; i < Math.Min(dbPassengers.Count, bbPassengers.Count); i++)
        {
            var dbPax = dbPassengers[i];
            var bbPax = bbPassengers[i];

            if (!string.IsNullOrWhiteSpace(bbPax.TicketNumber) && bbPax.TicketNumber != dbPax.TicketNumber)
                changes.Add(new BookingFieldChange(
                    $"Bilet No ({dbPax.FirstName} {dbPax.LastName})",
                    dbPax.TicketNumber ?? "-",
                    bbPax.TicketNumber));
        }

        return new SyncPreviewResult(changes.Count > 0, changes);
    }

    public async Task ConfirmAsync(Guid bookingId, Guid adminId, CancellationToken ct = default)
    {
        var preview = await PreviewAsync(bookingId, ct);
        if (!preview.HasChanges)
            throw new InvalidOperationException("BiletBank'ta değişiklik tespit edilmedi. DB güncellenmedi.");

        var booking = await _db.Bookings
            .Include(b => b.FlightSegments)
            .Include(b => b.Passengers)
            .FirstOrDefaultAsync(b => b.Id == bookingId, ct)!;

        var biletBank = await _flightService.ReadShoppingFileWithAutoLoginAsync(booking!.ShoppingFileId!, ct);

        if (biletBank.HasError)
            throw new InvalidOperationException($"BiletBank sorgusu başarısız: {biletBank.ErrorMessage}");

        // İptal
        if (biletBank.IsReservationCancelled && booking.Status != "Cancelled")
        {
            booking.Status = "Cancelled";
            booking.CancelledAt = DateTime.UtcNow;
        }

        // Segmentleri güncelle
        var dbSegments = booking.FlightSegments.OrderBy(s => s.SequenceNo).ToList();
        var bbSegments = biletBank.Segments;

        for (var i = 0; i < Math.Min(dbSegments.Count, bbSegments.Count); i++)
        {
            var dbSeg = dbSegments[i];
            var bbSeg = bbSegments[i];

            if (DateTime.TryParse(bbSeg.DepartureDay, out var bbDate))
                dbSeg.DepartureDate = DateTime.SpecifyKind(bbDate.Date, DateTimeKind.Utc);

            if (!string.IsNullOrWhiteSpace(bbSeg.FlightNumber))
                dbSeg.FlightNumber = bbSeg.FlightNumber;

            if (!string.IsNullOrWhiteSpace(bbSeg.DepartureTime))
                dbSeg.DepartureTime = bbSeg.DepartureTime;

            if (!string.IsNullOrWhiteSpace(bbSeg.ArrivalDay) && DateTime.TryParse(bbSeg.ArrivalDay, out var arrDate))
                dbSeg.ArrivalDate = DateTime.SpecifyKind(arrDate.Date, DateTimeKind.Utc);

            if (!string.IsNullOrWhiteSpace(bbSeg.ArrivalTime))
                dbSeg.ArrivalTime = bbSeg.ArrivalTime;
        }

        // Yolcu bilet numaralarını güncelle
        var dbPassengers = booking.Passengers.OrderBy(p => p.SequenceNo).ToList();

        for (var i = 0; i < Math.Min(dbPassengers.Count, biletBank.Passengers.Count); i++)
        {
            if (!string.IsNullOrWhiteSpace(biletBank.Passengers[i].TicketNumber))
                dbPassengers[i].TicketNumber = biletBank.Passengers[i].TicketNumber;
        }

        booking.UpdatedAt = DateTime.UtcNow;

        // Change log kayıtları
        foreach (var change in preview.Changes)
        {
            _db.BookingChangeLogs.Add(new BookingChangeLog
            {
                Id = Guid.NewGuid(),
                BookingId = bookingId,
                FieldName = change.FieldName,
                OldValue = change.OldValue,
                NewValue = change.NewValue,
                ChangedByAdminId = adminId,
                ChangedAt = DateTime.UtcNow
            });
        }

        // Operation log
        _db.BookingLogs.Add(new BookingLog
        {
            Id = Guid.NewGuid(),
            BookingId = bookingId,
            Operation = "AdminSync_BiletBank",
            IsSuccess = true,
            ResponseBody = $"Değişiklik sayısı: {preview.Changes.Count}",
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);

        // Email gönder
        await SendUpdateEmailAsync(booking, preview.Changes, ct);

        _logger.LogInformation("[BookingSyncService] Booking {BookingId} senkronize edildi. Değişiklik: {Count}", bookingId, preview.Changes.Count);
    }

    private async Task SendUpdateEmailAsync(Booking booking, List<BookingFieldChange> changes, CancellationToken ct)
    {
        try
        {
            var contactPassenger = booking.Passengers.OrderBy(p => p.SequenceNo).FirstOrDefault();
            if (contactPassenger == null || string.IsNullOrWhiteSpace(contactPassenger.Email))
            {
                _logger.LogWarning("[BookingSyncService] Booking {BookingId} için email adresi bulunamadı, email gönderilmedi.", booking.Id);
                return;
            }

            var pdfUrl = $"/api/Ticket/pdf/booking/{booking.Id}";

            await _emailService.SendBookingUpdatedAsync(
                toEmail: contactPassenger.Email,
                toName: $"{contactPassenger.FirstName} {contactPassenger.LastName}",
                pnr: booking.PNR ?? booking.InternalPnr ?? "-",
                bookingId: booking.Id,
                changes: changes,
                pdfDownloadUrl: pdfUrl,
                ct: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BookingSyncService] Email gönderilemedi. Booking: {BookingId}", booking.Id);
        }
    }
}
