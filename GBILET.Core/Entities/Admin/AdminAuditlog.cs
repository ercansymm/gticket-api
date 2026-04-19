namespace GBILET.Core.Entities.Admin;

public class AdminAuditLog
{
    public Guid Id { get; set; }

    // Kim (nullable — başarısız login denemesi için null)
    public Guid? AdminUserId { get; set; }
    public string? Username { get; set; }                  // Snapshot, user silinirse bile kalır

    // Ne
    public string Action { get; set; } = null!;            // "Login", "LoginFailed", "ViewBookings", "UpdateUser"
    public string? TargetEntity { get; set; }              // "AdminUser", "Booking", "PopularRoute"
    public string? TargetId { get; set; }                  // Etkilenen kaydın Id'si (string — Guid/int hepsi)
    public string? Details { get; set; }                   // JSON — ekstra bilgi (field değişiklikleri vb.)

    // Sonuç
    public bool Success { get; set; } = true;
    public string? ErrorMessage { get; set; }

    // Nereden
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    // Ne zaman
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation (nullable çünkü AdminUserId nullable)
    public AdminUser? AdminUser { get; set; }
}