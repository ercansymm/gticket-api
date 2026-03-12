using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GBILET.Core.Entities;

public class TripBooking
{
    public Guid Id { get; set; }
    public Guid TripId { get; set; }
    public Guid BookingId { get; set; }

    public Trip Trip { get; set; }
    public Booking Booking { get; set; }
}