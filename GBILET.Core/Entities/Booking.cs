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

    // Navigation properties
    public User User { get; set; }
    public List<Passenger> Passengers { get; set; } = new();
    public List<FlightSegment> FlightSegments { get; set; } = new();
    public List<Payment> Payments { get; set; } = new();
    public List<FareDetail> FareDetails { get; set; } = new();
    public BillingInfo? BillingInfo { get; set; }
    public List<BookingLog> BookingLogs { get; set; } = new();
}