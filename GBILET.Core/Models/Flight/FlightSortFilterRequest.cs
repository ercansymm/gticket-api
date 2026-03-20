namespace GBILET.Core.Models.Flight;

public class FlightSortRequest
{
    public string? SortBy { get; set; }
    public List<FlightResultDto> Flights { get; set; } = [];
}

public class FlightFilterRequest
{
    public bool? DirectOnly { get; set; }
    public bool? RefundableOnly { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public List<string>? AirlineCodes { get; set; }
    public string? DepartureTimeFrom { get; set; }
    public string? DepartureTimeTo { get; set; }
    public List<FlightResultDto> Flights { get; set; } = [];
}
