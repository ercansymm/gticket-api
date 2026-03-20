using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GBILET.Core.Entities;

public class BillingInfo
{
    public Guid Id { get; set; }
    public Guid BookingId { get; set; }
    public string BillingName { get; set; }
    public string? TaxNo { get; set; }
    public string? TaxOffice { get; set; }
    public string? AddressCity { get; set; }
    public string? AddressDistrict { get; set; }
    public string? AddressDetail { get; set; }
    public string? AddressZipCode { get; set; }
    public string? CountryCode { get; set; } = "TR";
    public bool IsCompany { get; set; } = false;

    public Booking Booking { get; set; }
}