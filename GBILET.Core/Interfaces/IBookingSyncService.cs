namespace GBILET.Core.Interfaces;

public record BookingFieldChange(string FieldName, string OldValue, string NewValue);

public record SyncPreviewResult(bool HasChanges, List<BookingFieldChange> Changes);

public interface IBookingSyncService
{
    Task<SyncPreviewResult> PreviewAsync(Guid bookingId, CancellationToken ct = default);
    Task ConfirmAsync(Guid bookingId, Guid adminId, CancellationToken ct = default);
}
