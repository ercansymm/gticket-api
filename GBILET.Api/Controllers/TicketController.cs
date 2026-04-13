using GBILET.Core.Models.Ticket;
using GBILET.Core.Service.Ticket;
using Microsoft.AspNetCore.Mvc;

namespace GBILET.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TicketController : ControllerBase
{
    private readonly ITicketPdfService _ticketPdfService;
    private readonly ILogger<TicketController> _logger;

    public TicketController(ITicketPdfService ticketPdfService, ILogger<TicketController> logger)
    {
        _ticketPdfService = ticketPdfService;
        _logger = logger;
    }

    [HttpGet("pdf/{shoppingFileId}")]
    public IActionResult GetPdf(string shoppingFileId)
    {
        try
        {
            // TODO: Replace with real data from ReadShoppingFile response / booking repository
            var data = new TicketPdfDataDto
            {
                PassengerName = "JOHN DOE",
                Pnr = "ABC123",
                TicketNumber = "235-1234567890",
                IssueDate = DateTime.Now,
                PassportOrTcNo = "12345678901",
                BaseFare = 1250.00m,
                Taxes = 350.00m,
                TotalFare = 1600.00m,
                Currency = "TRY",
                ContactPhone = "+90 850 123 45 67",
                ContactEmail = "destek@atabilet.com",
                Flights =
                [
                    new TicketFlightDto
                    {
                        AirlineName = "Turkish Airlines",
                        FlightCode = "TK2151",
                        BookingClass = "Y",
                        FareBasisName = "EcoFly",
                        OriginCity = "Ankara",
                        OriginAirport = "Esenboğa Havalimanı",
                        OriginCode = "ESB",
                        DepartureDate = "13 Nisan 2026",
                        DepartureTime = "08:30",
                        DestinationCity = "İstanbul",
                        DestinationAirport = "Sabiha Gökçen Havalimanı",
                        DestinationCode = "SAW",
                        ArrivalDate = "13 Nisan 2026",
                        ArrivalTime = "09:50",
                        BaggageAllowance = "20 KG"
                    }
                ]
            };

            _logger.LogInformation("Generating e-ticket PDF for shoppingFileId: {ShoppingFileId}", shoppingFileId);

            var pdfBytes = _ticketPdfService.GeneratePdf(data);

            return File(pdfBytes, "application/pdf", $"e-ticket-{shoppingFileId}.pdf");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating e-ticket PDF for shoppingFileId: {ShoppingFileId}", shoppingFileId);
            return StatusCode(500, new { error = "E-bilet PDF oluşturulurken bir hata oluştu." });
        }
    }
}
