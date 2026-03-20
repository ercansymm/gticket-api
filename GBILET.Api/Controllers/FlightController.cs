using GBILET.Core.Entities;
using GBILET.Core.Models.Flight;
using GBILET.Core.Service;
using GBILET.Core.Service.Flight;
using GBILET.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace GBILET.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FlightController : ControllerBase
{
    private readonly IFlightService _flightService;
    private readonly IBookingRepository _bookingRepository;
    private readonly IMemoryCache _cache;

    public FlightController(IFlightService flightService, IBookingRepository bookingRepository, IMemoryCache cache)
    {
        _flightService = flightService;
        _bookingRepository = bookingRepository;
        _cache = cache;
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

            var result = await _flightService.SearchFlightDtoAsync(request);
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

    [HttpPost("search/raw")]
    public async Task<IActionResult> SearchRaw([FromBody] SearchRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Origin) || string.IsNullOrWhiteSpace(request.Destination))
                return BadRequest(new { error = "Origin ve Destination alanları zorunludur." });

            if (request.DepartureDate == default)
                return BadRequest(new { error = "DepartureDate alanı zorunludur." });

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

    [HttpPost("search/sort")]
    public IActionResult Sort([FromBody] FlightSortRequest request)
    {
        try
        {
            if (request.Flights == null || request.Flights.Count == 0)
                return BadRequest(new { error = "Sıralanacak uçuş listesi boş." });

            var sorted = request.SortBy?.ToLowerInvariant() switch
            {
                "price" or "cheapest" => request.Flights.OrderBy(f => f.TotalFare).ToList(),
                "earliest" => request.Flights.OrderBy(f => f.DepartureTime).ToList(),
                "latest" => request.Flights.OrderByDescending(f => f.DepartureTime).ToList(),
                "shortest" or "duration" => request.Flights
                    .OrderBy(f => f.DurationHours * 60 + f.DurationMinutes).ToList(),
                _ => request.Flights
            };

            return Ok(sorted);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("search/filter")]
    public IActionResult Filter([FromBody] FlightFilterRequest request)
    {
        try
        {
            if (request.Flights == null || request.Flights.Count == 0)
                return BadRequest(new { error = "Filtrelenecek uçuş listesi boş." });

            var filtered = request.Flights.AsEnumerable();

            if (request.DirectOnly == true)
                filtered = filtered.Where(f => f.IsDirect);

            if (request.RefundableOnly == true)
                filtered = filtered.Where(f => f.IsRefundable);

            if (request.MinPrice.HasValue)
                filtered = filtered.Where(f => f.TotalFare >= request.MinPrice.Value);

            if (request.MaxPrice.HasValue)
                filtered = filtered.Where(f => f.TotalFare <= request.MaxPrice.Value);

            if (request.AirlineCodes is { Count: > 0 })
                filtered = filtered.Where(f => request.AirlineCodes.Contains(f.AirlineCode, StringComparer.OrdinalIgnoreCase));

            if (!string.IsNullOrEmpty(request.DepartureTimeFrom) && !string.IsNullOrEmpty(request.DepartureTimeTo))
            {
                filtered = filtered.Where(f =>
                    string.Compare(f.DepartureTime, request.DepartureTimeFrom, StringComparison.Ordinal) >= 0 &&
                    string.Compare(f.DepartureTime, request.DepartureTimeTo, StringComparison.Ordinal) <= 0);
            }

            var result = filtered.ToList();
            var filterOptions = FlightSearchMapper.BuildFilterOptions(result);

            return Ok(new { flights = result, filterOptions });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("session/{searchId}")]
    public IActionResult GetSession(string searchId)
    {
        var cacheKey = $"flight_session_{searchId}";
        if (_cache.TryGetValue<FlightSessionData>(cacheKey, out var sessionData) && sessionData != null)
        {
            return Ok(sessionData);
        }

        return NotFound(new { error = "Session bulunamadı veya süresi dolmuş." });
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

    [HttpPost("make-prebooking")]
    public async Task<IActionResult> MakePreBooking([FromBody] MakePreBookingRequest? request)
    {
        try
        {
            if (request == null)
                return BadRequest(new { error = "Request body parse edilemedi. JSON formatını kontrol edin." });

            if (string.IsNullOrWhiteSpace(request.SessionId) || string.IsNullOrWhiteSpace(request.SessionToken))
                return BadRequest(new { error = "SessionId ve SessionToken alanları zorunludur (Allocate response'tan alınır)." });

            if (string.IsNullOrWhiteSpace(request.ProductId))
                return BadRequest(new { error = "ProductId alanı zorunludur (Allocate response'taki AirBookings[0].ProductId)." });

            if (string.IsNullOrWhiteSpace(request.BrandedFareItemId))
                return BadRequest(new { error = "BrandedFareItemId alanı zorunludur (Allocate response'taki AirBookings[0].BrandedFareItems[0].BrandedFareItemId)." });

            if (string.IsNullOrWhiteSpace(request.ShoppingFileId))
                return BadRequest(new { error = "ShoppingFileId alanı zorunludur (Allocate response'taki ShoppingFileId)." });

            var result = await _flightService.MakePreBookingAsync(request);
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

            if (string.IsNullOrWhiteSpace(request.ProductItemId))
                return BadRequest(new { error = "ProductItemId alani zorunludur (Allocate response'taki BookingItems[].ProductItemId)." });

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

                if (string.IsNullOrWhiteSpace(pax.PaxReferenceId))
                    return BadRequest(new { error = $"Yolcu {pax.SequenceNo}: PaxReferenceId zorunludur (Allocate response'taki passengers[].paxReferenceId)." });

                // TempTag bos geldiyse PaxReferenceId'den otomatik doldur
                // BiletBank TempTag <-> PaxReferenceId eslesmesi bekliyor
                if (string.IsNullOrWhiteSpace(pax.TempTag))
                    pax.TempTag = pax.PaxReferenceId;
            }

            // BiletBank SOAP cagrilari (UpdatePassengers + MakePrebooking)
            var result = await _flightService.BookFlightAsync(request);

            // Ilk segmentten kalkis/varis ve havayolu bilgilerini al
            var firstSegment = result.Segments.FirstOrDefault();

            // Yolcu sayilarini hesapla
            int adultCount = request.Passengers.Count(p => p.PaxType == "ADT");
            int childCount = request.Passengers.Count(p => p.PaxType == "CHD");
            int infantCount = request.Passengers.Count(p => p.PaxType == "INF");

            // Kullanici tespiti:
            // - UserId verilmisse kayitli kullanici olarak isle
            // - Verilmemisse misafir oturumu olustur/bul (email uzerinden eslestirilir)
            Guid? resolvedUserId = null;
            Guid? resolvedGuestSessionId = null;

            if (request.UserId.HasValue && request.UserId.Value != Guid.Empty)
            {
                resolvedUserId = request.UserId.Value;
            }
            else
            {
                var contactEmail = request.Contact.Email;
                var existingGuest = await _bookingRepository.GetGuestSessionByEmailAsync(contactEmail);
                if (existingGuest != null)
                {
                    resolvedGuestSessionId = existingGuest.Id;
                }
                else
                {
                    var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                    var newGuest = await _bookingRepository.CreateGuestSessionAsync(new GuestSession
                    {
                        Id = Guid.NewGuid(),
                        Email = contactEmail,
                        Phone = request.Contact.Phone,
                        IpAddress = ipAddress,
                        CreatedAt = DateTime.UtcNow
                    });
                    resolvedGuestSessionId = newGuest.Id;
                }
            }

            // DB'ye booking kaydi olustur
            var bookingEntity = new Booking
            {
                Id = Guid.NewGuid(),
                UserId = resolvedUserId,
                GuestSessionId = resolvedGuestSessionId,
                BiletBankFileId = Guid.TryParse(result.ShoppingFileId, out var fileId) ? fileId : null,
                PNR = result.PNR,
                Status = result.HasError ? "Failed" : (result.Status ?? "Reserved"),
                GrandTotal = result.TotalFare,
                Currency = result.Currency ?? "TRY",
                IsFinalized = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                TransactionId = Guid.NewGuid().ToString(),
                SessionId = request.SessionId,
                SessionToken = request.SessionToken,
                ProductItemId = request.ProductItemId,
                Origin = firstSegment?.OriginCode,
                Destination = firstSegment?.DestinationCode,
                AirlineCode = firstSegment?.MarketingAirline,
                FlightNumber = firstSegment?.FlightNumber,
                BookedAt = DateTime.UtcNow,
                AdultCount = adultCount > 0 ? adultCount : 1,
                ChildCount = childCount,
                InfantCount = infantCount,
                LastError = result.HasError ? result.ErrorMessage : null
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
                    Phone = pax.SequenceNo == 1 ? request.Contact.Phone : null,
                    TempTag = pax.TempTag,
                    PaxReferenceId = pax.PaxReferenceId
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

            // UpdatePassengers log kaydi
            await _bookingRepository.AddLogAsync(new BookingLog
            {
                Id = Guid.NewGuid(),
                BookingId = bookingEntity.Id,
                SessionId = request.SessionId,
                SessionToken = request.SessionToken,
                Operation = "UpdatePassengers",
                IsSuccess = !result.HasError,
                ErrorMessage = result.HasError ? result.ErrorMessage : null,
                RequestBody = result.UpdatePassengersSoapRequest,
                ResponseBody = result.UpdatePassengersSoapResponse,
                CreatedAt = DateTime.UtcNow
            });

            // MakePrebooking log kaydi
            await _bookingRepository.AddLogAsync(new BookingLog
            {
                Id = Guid.NewGuid(),
                BookingId = bookingEntity.Id,
                SessionId = request.SessionId,
                SessionToken = request.SessionToken,
                Operation = "MakePrebooking",
                IsSuccess = !result.HasError,
                ErrorMessage = result.ErrorMessage,
                RequestBody = result.RawSoapRequest,
                ResponseBody = result.RawSoapResponse,
                CreatedAt = DateTime.UtcNow
            });

            // BiletBank response'ta yolcu bilgileri eksik gelebilir — request'ten tamamla
            foreach (var paxResult in result.Passengers)
            {
                var reqPax = request.Passengers.FirstOrDefault(p => p.SequenceNo == paxResult.SequenceNo);
                if (reqPax != null)
                {
                    paxResult.FirstName ??= reqPax.FirstName;
                    paxResult.LastName ??= reqPax.LastName;
                    paxResult.Gender ??= reqPax.Gender;
                    paxResult.BirthDate ??= reqPax.BirthDate;
                    paxResult.PaxType ??= reqPax.PaxType;
                }
            }

            // Response'ta hic yolcu yoksa request'ten olustur
            if (result.Passengers.Count == 0)
            {
                foreach (var reqPax in request.Passengers)
                {
                    result.Passengers.Add(new BookingPassengerResult
                    {
                        PaxType = reqPax.PaxType,
                        SequenceNo = reqPax.SequenceNo,
                        FirstName = reqPax.FirstName,
                        LastName = reqPax.LastName,
                        Gender = reqPax.Gender,
                        BirthDate = reqPax.BirthDate
                    });
                }
            }

            return Ok(new
            {
                result.PNR,
                result.Status,
                result.HasError,
                result.ErrorMessage,
                result.TotalFare,
                result.Currency,
                result.ShoppingFileId,
                result.Segments,
                result.Passengers,
                bookingId = bookingEntity.Id,
                userId = resolvedUserId,
                guestSessionId = resolvedGuestSessionId,
                isGuest = resolvedGuestSessionId.HasValue
            });
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