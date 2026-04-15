using GBILET.Core.Helpers;
using GBILET.Core.Models.Ticket;
using GBILET.Core.Service;
using GBILET.Core.Service.Ticket;
using Microsoft.AspNetCore.Mvc;

namespace GBILET.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TicketController : ControllerBase
{
    private readonly ITicketPdfService _ticketPdfService;
    private readonly IBookingRepository _bookingRepository;
    private readonly ILogger<TicketController> _logger;

    // Turkish IATA airport codes for international detection
    private static readonly HashSet<string> TurkishAirports = new(StringComparer.OrdinalIgnoreCase)
    {
        "SAW","IST","ESB","ADB","AYT","TZX","BJV","DLM","GZT","VAN","ERZ","EZS","DIY",
        "SZF","KYA","ASR","HTY","MLX","GNY","MZH","NOP","KCM","OGU","CKZ","BZC","DNZ",
        "ISE","MQM","NKT","SXZ","YKO","BAL","IGD","KSY","MSR","BGG","TJK","ONQ","AOE",
        "USQ","AFY","EDO","TEQ","BDM","KZR","ADA","NAV","GZP"
    };

    public TicketController(ITicketPdfService ticketPdfService, IBookingRepository bookingRepository, ILogger<TicketController> logger)
    {
        _ticketPdfService = ticketPdfService;
        _bookingRepository = bookingRepository;
        _logger = logger;
    }

    [HttpGet("pdf/{shoppingFileId}")]
    public async Task<IActionResult> GetPdf(string shoppingFileId, [FromQuery] int? sequenceNo = null)
    {
        try
        {
            _logger.LogInformation("Generating e-ticket PDF for shoppingFileId: {ShoppingFileId}, sequenceNo: {SequenceNo}", shoppingFileId, sequenceNo);

            var booking = await _bookingRepository.GetByShoppingFileIdAsync(shoppingFileId);
            if (booking == null)
            {
                _logger.LogWarning("No booking found for shoppingFileId: {ShoppingFileId}", shoppingFileId);
                return NotFound(new { error = "Rezervasyon bulunamadi." });
            }

            var fareDetail = booking.FareDetails.FirstOrDefault();
            var contactPax = booking.Passengers.FirstOrDefault(p => !string.IsNullOrEmpty(p.Email))
                ?? booking.Passengers.OrderBy(p => p.SequenceNo).FirstOrDefault();

            var totalFare = fareDetail?.GrandTotal ?? booking.GrandTotal ?? 0;
            var currency = fareDetail?.Currency ?? booking.Currency ?? "TRY";

            var segments = booking.FlightSegments
                .OrderBy(s => s.DepartureDate)
                .ThenBy(s => s.DepartureTime)
                .ToList();

            // International detection: any segment origin or destination is NOT Turkish
            var isInternational = segments.Any(s =>
                !TurkishAirports.Contains(s.OriginCode) || !TurkishAirports.Contains(s.DestinationCode));

            // Per-flight fare items: split total proportionally across segments
            var fareItems = new List<TicketFareItemDto>();
            if (segments.Count > 0)
            {
                var perSegment = Math.Round(totalFare / segments.Count, 2);
                var remainder = totalFare - (perSegment * segments.Count);

                for (var i = 0; i < segments.Count; i++)
                {
                    var seg = segments[i];
                    var amount = perSegment;
                    if (i == segments.Count - 1)
                        amount += remainder;

                    fareItems.Add(new TicketFareItemDto
                    {
                        Route = $"{FlightMappings.GetAirportName(seg.OriginCode)} ({seg.OriginCode}) → {FlightMappings.GetAirportName(seg.DestinationCode)} ({seg.DestinationCode})",
                        Amount = amount,
                        Currency = currency
                    });
                }
            }

            var flightDtos = segments
                .Select(seg => new TicketFlightDto
                {
                    AirlineName = FlightMappings.GetAirlineName(seg.MarketingAirline),
                    FlightCode = FormatFlightCode(seg.MarketingAirline, seg.FlightNumber),
                    BookingClass = seg.BookingClass ?? "",
                    FareBasisName = seg.FareBasis,
                    OriginCity = FlightMappings.GetAirportName(seg.OriginCode),
                    OriginAirport = FlightMappings.GetAirportName(seg.OriginCode),
                    OriginCode = seg.OriginCode,
                    DepartureDate = FormatDateTurkish(seg.DepartureDate),
                    DepartureTime = seg.DepartureTime ?? "",
                    DestinationCity = FlightMappings.GetAirportName(seg.DestinationCode),
                    DestinationAirport = FlightMappings.GetAirportName(seg.DestinationCode),
                    DestinationCode = seg.DestinationCode,
                    ArrivalDate = seg.ArrivalDate.HasValue ? FormatDateTurkish(seg.ArrivalDate.Value) : FormatDateTurkish(seg.DepartureDate),
                    ArrivalTime = seg.ArrivalTime ?? "",
                    BaggageAllowance = seg.Baggage ?? "—",
                    AirlineCode = seg.MarketingAirline ?? ""
                })
                .ToList();

            // Build per-passenger data list (filter by sequenceNo if provided)
            var allPassengers = booking.Passengers.OrderBy(p => p.SequenceNo).ToList();
            if (sequenceNo.HasValue)
            {
                allPassengers = allPassengers.Where(p => p.SequenceNo == sequenceNo.Value).ToList();
                if (allPassengers.Count == 0)
                {
                    return NotFound(new { error = "Belirtilen yolcu bulunamadi." });
                }
            }
            var passengerDataList = allPassengers.Select(pax => new TicketPdfDataDto
            {
                PassengerName = $"{pax.FirstName} {pax.LastName}",
                Pnr = booking.InternalPnr ?? booking.PNR ?? "—",
                TicketNumber = pax.TicketNumber ?? "—",
                IssueDate = booking.TicketedAt ?? booking.PaidAt ?? booking.CreatedAt,
                TcNo = pax.CitizenNo,
                PassportNo = pax.PassportNo,
                PassportCountry = pax.PassportCountry,
                IsInternational = isInternational,
                BaseFare = fareDetail?.BaseFare ?? 0,
                Taxes = fareDetail?.TotalTax ?? 0,
                TotalFare = totalFare,
                Currency = currency,
                FareItems = fareItems,
                ContactPhone = contactPax?.Phone ?? "",
                ContactEmail = contactPax?.Email ?? "",
                Flights = flightDtos
            }).ToList();

            var pdfBytes = _ticketPdfService.GeneratePdf(passengerDataList);

            return File(pdfBytes, "application/pdf", $"e-ticket-{shoppingFileId}.pdf");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating e-ticket PDF for shoppingFileId: {ShoppingFileId}", shoppingFileId);
            return StatusCode(500, new { error = "E-bilet PDF oluşturulurken bir hata oluştu." });
        }
    }

    private static string FormatFlightCode(string airline, string flightNo)
    {
        if (string.IsNullOrEmpty(flightNo)) return airline ?? "";
        if (string.IsNullOrEmpty(airline)) return flightNo;
        if (flightNo.StartsWith(airline)) return flightNo;
        return $"{airline} {flightNo}";
    }

    private static string FormatDateTurkish(DateTime date)
    {
        string[] months = ["", "Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran",
            "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık"];
        return $"{date.Day} {months[date.Month]} {date.Year}";
    }
}
