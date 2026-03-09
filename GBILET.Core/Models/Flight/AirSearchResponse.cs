namespace GBILET.Core.Models.Flight;

public class AirSearchResponse
{
    public bool HasError { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SearchId { get; set; }
    public string? ShoppingFileId { get; set; }

    /// <summary>
    /// Oturum bilgisi - Allocate icin bu degeri kullanin
    /// </summary>
    public string? SessionId { get; set; }
    public string? SessionToken { get; set; }

    public List<FlightOption> FlightOptions { get; set; } = [];
    public List<RecommendationBox> RecommendationBoxes { get; set; } = [];
}

public class FlightOption
{
    public string? ProductId { get; set; }
    public string? ProductItemId { get; set; }
    public string? ProviderId { get; set; }
    public string? Type { get; set; }
    public string? BookingCode { get; set; }
    public string? Currency { get; set; }
    public string? ExchangeCode { get; set; }
    public decimal BaseFare { get; set; }
    public decimal Taxes { get; set; }
    public decimal ServiceFee { get; set; }
    public decimal SystemServiceFee { get; set; }
    public decimal EndSellerCommission { get; set; }
    public decimal NetFare { get; set; }
    public decimal TotalFare { get; set; }
    public bool IsRefundable { get; set; }
    public bool IsReservable { get; set; }
    public bool IsGetFareRulesEnabled { get; set; }
    public string? OptionFlag { get; set; }
    public string? BookingProvider { get; set; }
    public string? BookingProviderId { get; set; }
    public int Duration { get; set; }
    public List<FlightSegment> Segments { get; set; } = [];
    public List<SegmentAvailability> SegmentAvailabilities { get; set; } = [];
    public List<PassengerFareItem> PassengerFareItems { get; set; } = [];
    public List<BrandedFareItem> BrandedFareItems { get; set; } = [];
    public List<FreeBaggageAllowance> FreeBaggageAllowances { get; set; } = [];
}

public class FlightSegment
{
    public string? SegmentId { get; set; }
    public int SequenceNo { get; set; }
    public string? OriginCode { get; set; }
    public string? DestinationCode { get; set; }
    public string? OD_OriginCode { get; set; }
    public string? OD_DestinationCode { get; set; }
    public string? DepartureDay { get; set; }
    public string? DepartureTime { get; set; }
    public string? ArrivalDay { get; set; }
    public string? ArrivalTime { get; set; }
    public string? MarketingAirline { get; set; }
    public string? OperatingAirline { get; set; }
    public string? FlightNumber { get; set; }
    public string? BookingClass { get; set; }
    public string? FareBasis { get; set; }
    public string? FareType { get; set; }
    public string? Equipment { get; set; }
    public int Duration { get; set; }
    public string? SelectedBrandedFareItemId { get; set; }
}

public class SegmentAvailability
{
    public int SegmentSequenceNo { get; set; }
    public string? BookingClassCode { get; set; }
    public int AvailableSeats { get; set; }
    public bool IsPromo { get; set; }
}

public class PassengerFareItem
{
    public string? PaxCode { get; set; }
    public int PaxSequence { get; set; }
    public string? ProductItemId { get; set; }
    public string? Currency { get; set; }
    public decimal BaseFare { get; set; }
    public decimal Taxes { get; set; }
    public decimal ServiceFee { get; set; }
    public decimal SystemServiceFee { get; set; }
    public decimal EndSellerCommission { get; set; }
    public decimal TotalFare { get; set; }
    public CustomerCommission? CustomerCommission { get; set; }
}

public class CustomerCommission
{
    public decimal Minimum { get; set; }
    public decimal Maximum { get; set; }
    public decimal Value { get; set; }
}

public class BrandedFareItem
{
    public string? BrandedFareItemId { get; set; }
    public List<BrandedFarePassenger> BrandedFarePassengers { get; set; } = [];
    public BrandedFareTotalInfo? TotalFareInfo { get; set; }
    public List<BrandedItem> BrandedItems { get; set; } = [];
}

public class BrandedFarePassenger
{
    public int PassengerCount { get; set; }
    public string? PassengerType { get; set; }
    public List<FareComponent> FareComponents { get; set; } = [];
    public PassengerFareInfo? PassengerFareInfo { get; set; }
    public FarePolicy? Policy { get; set; }
}

public class FareComponent
{
    public string? BrandId { get; set; }
    public string? BookingClass { get; set; }
    public string? CabinClass { get; set; }
    public string? FareBasisCode { get; set; }
    public string? FreeBaggageAllowanceId { get; set; }
    public int AvailableSeats { get; set; }
    public string? SegmentId { get; set; }
}

public class PassengerFareInfo
{
    public decimal BaseFare { get; set; }
    public decimal Taxes { get; set; }
    public decimal TotalFare { get; set; }
    public string? Currency { get; set; }
    public int PaxSequence { get; set; }
    public string? PaxType { get; set; }
}

public class FarePolicy
{
    public List<CancellationPolicy> CancellationPolicies { get; set; } = [];
    public List<ChangePolicy> ChangePolicies { get; set; } = [];
}

public class CancellationPolicy
{
    public decimal Amount { get; set; }
    public string? Applicability { get; set; } // BeforeDeparture, AfterDeparture
    public int MinutesToDeparture { get; set; }
    public string? Currency { get; set; }
    public bool IsRefundable { get; set; }
}

public class ChangePolicy
{
    public decimal Amount { get; set; }
    public string? Applicability { get; set; } // BeforeDeparture, AfterDeparture
    public int MinutesToDeparture { get; set; }
    public string? Currency { get; set; }
    public bool IsChangeable { get; set; }
}

public class BrandedFareTotalInfo
{
    public decimal TotalFare { get; set; }
    public decimal TotalTaxes { get; set; }
}

public class BrandedItem
{
    public string? BrandCode { get; set; }
    public string? BrandId { get; set; }
    public string? BrandName { get; set; }
    public List<BrandedRule> BrandedRules { get; set; } = [];
}

public class BrandedRule
{
    public string? Application { get; set; } // N, F, C
    public string? DisplayType { get; set; }
    public string? RuleDescription { get; set; }
    public string? ServiceGroup { get; set; }
}

public class FreeBaggageAllowance
{
    public string? Allowance { get; set; }
    public string? Category { get; set; } // Checked, Cabin
    public string? Type { get; set; } // Weight, Piece
    public string? Unit { get; set; } // K (Kilogram), N (Piece)
    public string? PaxType { get; set; }
}

public class RecommendationBox
{
    public string? ProductId { get; set; }
    public string? FlightId { get; set; }
    public string? Currency { get; set; }
    public decimal BaseFare { get; set; }
    public decimal Taxes { get; set; }
    public decimal ServiceFee { get; set; }
    public decimal TotalFare { get; set; }
    public List<RecommendationFlight> OutboundFlights { get; set; } = [];
    public List<RecommendationFlight> InboundFlights { get; set; } = [];
    public List<BrandedFareItem> BrandedFareItems { get; set; } = [];
}

public class RecommendationFlight
{
    public string? FlightId { get; set; }
    public List<FlightSegment> Segments { get; set; } = [];
    public int Duration { get; set; }
}
