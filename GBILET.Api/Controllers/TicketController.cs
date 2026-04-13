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

    public TicketController(ITicketPdfService ticketPdfService, IBookingRepository bookingRepository, ILogger<TicketController> logger)
    {
        _ticketPdfService = ticketPdfService;
        _bookingRepository = bookingRepository;
        _logger = logger;
    }

    [HttpGet("pdf/{shoppingFileId}")]
    public async Task<IActionResult> GetPdf(string shoppingFileId)
    {
        try
        {
            _logger.LogInformation("Generating e-ticket PDF for shoppingFileId: {ShoppingFileId}", shoppingFileId);

            var booking = await _bookingRepository.GetByShoppingFileIdAsync(shoppingFileId);
            if (booking == null)
            {
                _logger.LogWarning("No booking found for shoppingFileId: {ShoppingFileId}", shoppingFileId);
                return NotFound(new { error = "Rezervasyon bulunamadi." });
            }

            var firstPax = booking.Passengers.OrderBy(p => p.SequenceNo).FirstOrDefault();
            var fareDetail = booking.FareDetails.FirstOrDefault();
            var contactPax = booking.Passengers.FirstOrDefault(p => !string.IsNullOrEmpty(p.Email)) ?? firstPax;

            var data = new TicketPdfDataDto
            {
                PassengerName = firstPax != null ? $"{firstPax.FirstName} {firstPax.LastName}" : "—",
                Pnr = booking.PNR ?? "—",
                TicketNumber = firstPax?.TicketNumber ?? "—",
                IssueDate = booking.TicketedAt ?? booking.PaidAt ?? booking.CreatedAt,
                PassportOrTcNo = firstPax?.CitizenNo ?? firstPax?.PassportNo,
                BaseFare = fareDetail?.BaseFare ?? 0,
                Taxes = fareDetail?.TotalTax ?? 0,
                TotalFare = fareDetail?.GrandTotal ?? booking.GrandTotal ?? 0,
                Currency = fareDetail?.Currency ?? booking.Currency ?? "TRY",
                ContactPhone = contactPax?.Phone ?? "",
                ContactEmail = contactPax?.Email ?? "",
                Flights = booking.FlightSegments
                    .OrderBy(s => s.DepartureDate)
                    .ThenBy(s => s.DepartureTime)
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
                        BaggageAllowance = seg.Baggage ?? "—"
                    })
                    .ToList()
            };

            var pdfBytes = _ticketPdfService.GeneratePdf(data);

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
