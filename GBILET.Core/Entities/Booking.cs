using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GBILET.Core.Entities;

public class Booking
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public Guid? BiletBankFileId { get; set; }
    public string? PNR { get; set; }
    public string? InternalPnr { get; set; }
    public string Status { get; set; } = "Created";
    public decimal? GrandTotal { get; set; }
    public string? Currency { get; set; } = "TRY";
    public bool IsFinalized { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Benzersiz işlem takip ID'si
    public string? TransactionId { get; set; }

    // Hızlı erişim için kalkış/varış
    public string? Origin { get; set; }
    public string? Destination { get; set; }

    // Havayolu
    public string? AirlineCode { get; set; }
    public string? FlightNumber { get; set; }

    // BiletBank ek referanslar
    public string? SessionId { get; set; }
    public string? SessionToken { get; set; }
    public string? ProductItemId { get; set; }

    // Komisyon
    public decimal ServiceFee { get; set; } = 0;
    public decimal OurCommission { get; set; } = 0;

    // Adım zaman damgaları
    public DateTime? AllocatedAt { get; set; }
    public DateTime? BookedAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime? TicketedAt { get; set; }
    public DateTime? CancelledAt { get; set; }

    /// <summary>
    /// Biletleme son tarihi (TKTL / Reservation_ExpiresAt). Bu süre dolduğunda PNR
    /// havayolu/GDS tarafında otomatik düşer ve koltuk yeniden satışa açılır.
    /// MakePreBooking response'undaki TimeTable.Reservation_ExpiresAt'ten gelir.
    /// </summary>
    public DateTime? TicketTimeLimit { get; set; }

    // Hata takibi
    public string? LastError { get; set; }
    public int RetryCount { get; set; } = 0;

    // Yolcu sayıları
    public int AdultCount { get; set; } = 1;
    public int ChildCount { get; set; } = 0;
    public int InfantCount { get; set; } = 0;

    /// <summary>
    /// Üye olmadan bilet alan misafir kullanıcının oturum ID'si.
    /// UserId null ise bu alan dolu olur.
    /// </summary>
    public Guid? GuestSessionId { get; set; }

    // Navigation properties
    public User? User { get; set; }
    public GuestSession? GuestSession { get; set; }
    public List<Passenger> Passengers { get; set; } = new();
    public List<FlightSegment> FlightSegments { get; set; } = new();
    public List<Payment> Payments { get; set; } = new();
    public List<FareDetail> FareDetails { get; set; } = new();
    public BillingInfo? BillingInfo { get; set; }
    public List<BookingLog> BookingLogs { get; set; } = new();


    
     
    


}