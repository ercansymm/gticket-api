using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GBILET.Core.Entities;

public class FareDetail
{
    public Guid Id { get; set; }
    public Guid BookingId { get; set; }
    public decimal BaseFare { get; set; }
    public decimal TotalTax { get; set; }
    public decimal ServiceFee { get; set; } = 0;
    public decimal GrandTotal { get; set; }
    public string Currency { get; set; } = "TRY";
    public decimal? BiletBankCost { get; set; }
    public decimal? OurPrice { get; set; }
    public decimal? Profit { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Booking Booking { get; set; }
}