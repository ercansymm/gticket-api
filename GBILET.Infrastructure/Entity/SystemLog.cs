using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GBILET.Infrastructure.Entity;

public class SystemLog
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(50)]
    public string? Level { get; set; } // Info, Warning, Error, Critical

    [MaxLength(200)]
    public string? Source { get; set; } // Hangi servis/controller

    [MaxLength(500)]
    public string? Message { get; set; }

    public string? Details { get; set; } // Stack trace veya detaylı bilgi

    public Guid? UserId { get; set; }

    [MaxLength(50)]
    public string? IpAddress { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
