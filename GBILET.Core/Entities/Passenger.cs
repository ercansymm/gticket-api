using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GBILET.Core.Entities;

public class Passenger
{
    public Guid Id { get; set; }
    public Guid BookingId { get; set; }
    public int SequenceNo { get; set; }
    public string Type { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Gender { get; set; }
    public string BirthDate { get; set; }
    public string? CitizenNo { get; set; }
    public string? PassportNo { get; set; }
    public string? PassportCountry { get; set; }
    public string? Nationality { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? HesCode { get; set; }

    public Booking Booking { get; set; }
}