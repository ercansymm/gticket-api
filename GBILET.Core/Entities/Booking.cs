using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GBILET.Core.Entities;

public class Booking
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? BiletBankFileId { get; set; }
    public string? PNR { get; set; }
    public string Status { get; set; } = "Created";
    public decimal? GrandTotal { get; set; }
    public string? Currency { get; set; } = "TRY";
    public bool IsFinalized { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; }
}