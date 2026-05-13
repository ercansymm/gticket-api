namespace GBILET.Core.Entities;

public class BookingChangeLog
{
    public Guid Id { get; set; }
    public Guid BookingId { get; set; }
    public string FieldName { get; set; } = "";
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public Guid? ChangedByAdminId { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

    public Booking Booking { get; set; } = null!;
}
