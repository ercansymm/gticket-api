using GBILET.Core.Entities;
using GBILET.Core.Models.Flight;
using GBILET.Core.Service;
using GBILET.Core.Service.Flight;
using Microsoft.AspNetCore.Mvc;

namespace GBILET.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FlightController : ControllerBase
{
    private readonly IFlightService _flightService;
    private readonly IBookingRepository _bookingRepository;

    public FlightController(IFlightService flightService, IBookingRepository bookingRepository)
    {
        _flightService = flightService;
        _bookingRepository = bookingRepository;
    }

    [HttpPost("search")]
    public async Task<IActionResult> Search([FromBody] SearchRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Origin) || string.IsNullOrWhiteSpace(request.Destination))
                return BadRequest(new { error = "Origin ve Destination alanları zorunludur." });

            if (request.DepartureDate == default)
                return BadRequest(new { error = "DepartureDate alanı zorunludur." });

            if (request.FlightType == "RT" && !request.ReturnDate.HasValue)
                return BadRequest(new { error = "Gidiş-dönüş uçuşlar için ReturnDate zorunludur." });

            if (request.AdultCount + request.ChildCount > 9)
                return BadRequest(new { error = "Bebek hariç toplam yolcu sayısı 9'u geçemez." });

            if (request.InfantCount > request.AdultCount)
                return BadRequest(new { error = "Bebek sayısı yetişkin sayısını geçemez." });

            var result = await _flightService.SearchFlightAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                error = ex.Message,
                inner = ex.InnerException?.Message
            });
        }
    }

    [HttpPost("allocate")]
    public async Task<IActionResult> Allocate([FromBody] AllocateRequest? request)
    {
        try
        {
            if (request == null)
                return BadRequest(new { error = "Request body parse edilemedi. JSON formatını kontrol edin." });

            if (string.IsNullOrWhiteSpace(request.ProductId))
                return BadRequest(new { error = "ProductId alanı zorunludur. Search sonucundan bir FlightOption.ProductId seçin." });

            // Session yoksa SearchRequest zorunlu
            var hasSession = !string.IsNullOrEmpty(request.SessionId) && !string.IsNullOrEmpty(request.SessionToken);
            if (!hasSession && request.SearchRequest == null)
                return BadRequest(new { error = "SessionId/SessionToken verilmediyse SearchRequest zorunludur." });

            var result = await _flightService.AllocateFlightAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                error = ex.Message,
                inner = ex.InnerException?.Message
            });
        }
    }

    [HttpPost("book")]
    public async Task<IActionResult> Book([FromBody] BookingRequest? request)
    {
        try
        {
            if (request == null)
                return BadRequest(new { error = "Request body parse edilemedi. JSON formatini kontrol edin." });

            if (string.IsNullOrWhiteSpace(request.SessionId) || string.IsNullOrWhiteSpace(request.SessionToken))
                return BadRequest(new { error = "SessionId ve SessionToken alanlari zorunludur (Allocate response'tan alinir)." });

            if (string.IsNullOrWhiteSpace(request.ShoppingFileId))
                return BadRequest(new { error = "ShoppingFileId alani zorunludur (Allocate response'tan alinir)." });

            if (string.IsNullOrWhiteSpace(request.ProductId))
                return BadRequest(new { error = "ProductId alani zorunludur (Allocate response'taki T_AirBooking.ProductId)." });

            if (request.Passengers == null || request.Passengers.Count == 0)
                return BadRequest(new { error = "En az bir yolcu bilgisi girilmelidir." });

            if (request.Contact == null || string.IsNullOrWhiteSpace(request.Contact.Email) || string.IsNullOrWhiteSpace(request.Contact.Phone))
                return BadRequest(new { error = "Iletisim bilgileri (Email ve Phone) zorunludur." });

            foreach (var pax in request.Passengers)
            {
                if (string.IsNullOrWhiteSpace(pax.FirstName) || string.IsNullOrWhiteSpace(pax.LastName))
                    return BadRequest(new { error = $"Yolcu {pax.SequenceNo}: Ad ve soyad zorunludur." });

                if (string.IsNullOrWhiteSpace(pax.BirthDate))
                    return BadRequest(new { error = $"Yolcu {pax.SequenceNo}: Dogum tarihi zorunludur." });

                if (string.IsNullOrWhiteSpace(pax.Gender))
                    return BadRequest(new { error = $"Yolcu {pax.SequenceNo}: Cinsiyet (M/F) zorunludur." });
            }

            // BiletBank SOAP cagrilari (UpdatePassengers + MakePrebooking)
            var result = await _flightService.BookFlightAsync(request);

            // DB'ye booking kaydi olustur
            var bookingEntity = new Booking
            {
                Id = Guid.NewGuid(),
                BiletBankFileId = Guid.TryParse(result.ShoppingFileId, out var fileId) ? fileId : null,
                PNR = result.PNR,
                Status = result.HasError ? "Failed" : (result.Status ?? "Reserved"),
                GrandTotal = result.TotalFare,
                Currency = result.Currency ?? "TRY",
                IsFinalized = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Yolcu kayitlari
            foreach (var pax in request.Passengers)
            {
                bookingEntity.Passengers.Add(new Passenger
                {
                    Id = Guid.NewGuid(),
                    BookingId = bookingEntity.Id,
                    SequenceNo = pax.SequenceNo,
                    Type = pax.PaxType,
                    FirstName = pax.FirstName,
                    LastName = pax.LastName,
                    Gender = pax.Gender,
                    BirthDate = pax.BirthDate,
                    CitizenNo = pax.CitizenNo,
                    PassportNo = pax.PassportNo,
                    PassportCountry = pax.PassportCountry,
                    Nationality = pax.Nationality,
                    Email = pax.SequenceNo == 1 ? request.Contact.Email : null,
                    Phone = pax.SequenceNo == 1 ? request.Contact.Phone : null
                });
            }

            // Segment kayitlari (BiletBank response'tan)
            foreach (var seg in result.Segments)
            {
                bookingEntity.FlightSegments.Add(new Core.Entities.FlightSegment
                {
                    Id = Guid.NewGuid(),
                    BookingId = bookingEntity.Id,
                    SequenceNo = seg.SequenceNo,
                    MarketingAirline = seg.MarketingAirline ?? "",
                    FlightNumber = seg.FlightNumber ?? "",
                    OriginCode = seg.OriginCode ?? "",
                    DestinationCode = seg.DestinationCode ?? "",
                    DepartureDate = DateTime.TryParse(seg.DepartureDay, out var depDate) ? depDate : DateTime.MinValue,
                    DepartureTime = seg.DepartureTime,
                    ArrivalDate = DateTime.TryParse(seg.ArrivalDay, out var arrDate) ? arrDate : null,
                    ArrivalTime = seg.ArrivalTime,
                    BookingClass = seg.BookingClass
                });
            }

            await _bookingRepository.CreateBookingAsync(bookingEntity);

            // Booking log kaydi
            await _bookingRepository.AddLogAsync(new BookingLog
            {
                Id = Guid.NewGuid(),
                BookingId = bookingEntity.Id,
                SessionId = request.SessionId,
                SessionToken = request.SessionToken,
                Operation = "MakePrebooking",
                IsSuccess = !result.HasError,
                ErrorMessage = result.ErrorMessage,
                ResponseBody = result.RawSoapResponse,
                CreatedAt = DateTime.UtcNow
            });

            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                error = ex.Message,
                inner = ex.InnerException?.Message
            });
        }
    }
}