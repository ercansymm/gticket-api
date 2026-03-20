using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GBILET.Core.Entities;

public class BookingLog
{
    public Guid Id { get; set; }
    public Guid? BookingId { get; set; }
    public Guid? UserId { get; set; }
    public string? SessionId { get; set; }
    public string? SessionToken { get; set; }
    public string Operation { get; set; }
    public string? RequestBody { get; set; }
    public string? ResponseBody { get; set; }
    public bool IsSuccess { get; set; } = false;
    public string? ErrorMessage { get; set; }
    public string? IpAddress { get; set; }

    // Performans takibi
    public int? ResponseTimeMs { get; set; }

    // HTTP detayları
    public int? HttpStatusCode { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Booking? Booking { get; set; }
    public User? User { get; set; }
}