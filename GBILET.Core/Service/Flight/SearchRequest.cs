using System;
namespace GBILET.Core.Models.Flight;

public class SearchRequest
{
    public string Origin { get; set; }
    public string Destination { get; set; }
    public string OriginCountryCode { get; set; } = "TR";
    public string DestinationCountryCode { get; set; } = "TR";
    public bool OriginIsCity { get; set; } = false;
    public bool DestinationIsCity { get; set; } = false;
    public DateTime DepartureDate { get; set; }
    public DateTime? ReturnDate { get; set; }       
    public string FlightType { get; set; } = "OW"; // OW, RT, MP
    public string FlightClass { get; set; } = "Economy"; // Economy, Business, First, Comfort
    public int AdultCount { get; set; } = 1;
    public int ChildCount { get; set; } = 0;
    public int InfantCount { get; set; } = 0;
    public bool DirectFlightsOnly { get; set; } = false;
    public bool RefundablesOnly { get; set; } = false;
    public int SearchTimeoutMilliseconds { get; set; } = 0;
    public List<string>? PreferredAirlines { get; set; }
    public string SearchReason { get; set; } = "SearchAndBook"; // SearchOnly, SearchAndBook
}   