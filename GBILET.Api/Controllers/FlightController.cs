using GBILET.Core.Entities;
using GBILET.Core.Models.Flight;
using GBILET.Core.Service;
using GBILET.Core.Service.Flight;
using GBILET.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using GBILET.Core.Entities;

namespace GBILET.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FlightController : ControllerBase
{
    private readonly IFlightService _flightService;
    private readonly IBookingRepository _bookingRepository;
    private readonly IMemoryCache _cache;
    private readonly ILogger<FlightController> _logger;

    public FlightController(IFlightService flightService, IBookingRepository bookingRepository, IMemoryCache cache, ILogger<FlightController> logger)
    {
        _flightService = flightService;
        _bookingRepository = bookingRepository;
        _cache = cache;
        _logger = logger;
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

    [HttpPost("update-passengers")]
    public async Task<IActionResult> UpdatePassengers([FromBody] UpdatePassengersRequest? request)
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
                return BadRequest(new { error = "ProductId alani zorunludur (Allocate response'taki AirBookings[0].ProductId)." });

            if (string.IsNullOrWhiteSpace(request.ProductItemId))
                return BadRequest(new { error = "ProductItemId alani zorunludur (Allocate response'taki BookingItems[0].ProductItemId)." });

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

                if (string.IsNullOrWhiteSpace(pax.TempTag))
                    pax.TempTag = pax.PaxReferenceId;
            }

            var result = await _flightService.UpdatePassengersAsync(request);
            return Ok(new { result.HasError, result.ErrorMessage });
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
                return BadRequest(new { error = "Request body parse edilemedi. JSON formatini kontrol edin." });

            if (string.IsNullOrWhiteSpace(request.SessionId) || string.IsNullOrWhiteSpace(request.SessionToken))
                return BadRequest(new { error = "SessionId ve SessionToken alanlari zorunludur (Allocate response'tan alinir)." });

            if (string.IsNullOrWhiteSpace(request.ProductId))
                return BadRequest(new { error = "ProductId alani zorunludur (Allocate response'taki AirBookings[0].ProductId)." });

            if (string.IsNullOrWhiteSpace(request.BrandedFareItemId))
                return BadRequest(new { error = "BrandedFareItemId alani zorunludur (Allocate response'taki AirBookings[0].BrandedFareItems[0].BrandedFareItemId)." });

            if (string.IsNullOrWhiteSpace(request.ShoppingFileId))
                return BadRequest(new { error = "ShoppingFileId alani zorunludur (Allocate response'taki ShoppingFileId)." });

            if (request.Passengers == null || request.Passengers.Count == 0)
                return BadRequest(new { error = "En az bir yolcu bilgisi girilmelidir (DB kaydı icin gerekli)." });

            if (request.Contact == null || string.IsNullOrWhiteSpace(request.Contact.Email))
                return BadRequest(new { error = "Iletisim bilgisi (Email) zorunludur." });

            var result = await _flightService.MakePreBookingAsync(request);

            Console.WriteLine($">>> RESULT: HasError={result.HasError}, BookingCode={result.BookingCode}, Status={result.Status}");

            if (result.HasError)
                return Ok(result);

            if (string.IsNullOrEmpty(result.BookingCode))
            {
                Console.WriteLine($">>> BOOKING CODE EMPTY, skipping DB write. BookingCode='{result.BookingCode}'");
                return Ok(result);
            }

            // Basarili prebooking — DB'ye booking kaydi olustur
            Guid? resolvedUserId = null;
            Guid? resolvedGuestSessionId = null;
            Guid? savedBookingId = null;

            try
            {
                Console.WriteLine(">>> DB WRITE START");

                if (request.UserId.HasValue && request.UserId.Value != Guid.Empty)
                {
                    resolvedUserId = request.UserId.Value;
                }
                else
                {
                    var existingGuest = await _bookingRepository.GetGuestSessionByEmailAsync(request.Contact.Email);
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
                            Email = request.Contact.Email,
                            Phone = request.Contact.Phone,
                            IpAddress = ipAddress,
                            CreatedAt = DateTime.UtcNow
                        });
                        resolvedGuestSessionId = newGuest.Id;
                    }
                }

                var firstSegment = result.Segments.FirstOrDefault();
                int adultCount = request.Passengers.Count(p => p.PaxType == "ADT");
                int childCount = request.Passengers.Count(p => p.PaxType == "CHD");
                int infantCount = request.Passengers.Count(p => p.PaxType == "INF");

                var bookingEntity = new Booking
                {
                    Id = Guid.NewGuid(),
                    UserId = resolvedUserId,
                    GuestSessionId = resolvedGuestSessionId,
                    BiletBankFileId = Guid.TryParse(result.ShoppingFileId, out var fileId) ? fileId : null,
                    PNR = result.BookingCode,
                    Status = result.Status ?? "PreBooked",
                    GrandTotal = result.TotalFare,
                    Currency = result.Currency ?? "TRY",
                    ServiceFee = result.ServiceFee,
                    IsFinalized = false,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    TransactionId = Guid.NewGuid().ToString(),
                    SessionId = request.SessionId,
                    SessionToken = request.SessionToken,
                    Origin = firstSegment?.OriginCode,
                    Destination = firstSegment?.DestinationCode,
                    AirlineCode = firstSegment?.MarketingAirline,
                    FlightNumber = firstSegment?.FlightNumber,
                    BookedAt = DateTime.UtcNow,
                    AdultCount = adultCount > 0 ? adultCount : 1,
                    ChildCount = childCount,
                    InfantCount = infantCount
                };

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

                foreach (var seg in result.Segments)
                {
                    bookingEntity.FlightSegments.Add(new GBILET.Core.Entities.FlightSegment
                    {
                        Id = Guid.NewGuid(),
                        BookingId = bookingEntity.Id,
                        SequenceNo = 0,
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

                bookingEntity.BookingLogs.Add(new BookingLog
                {
                    Id = Guid.NewGuid(),
                    BookingId = bookingEntity.Id,
                    SessionId = request.SessionId,
                    SessionToken = request.SessionToken,
                    Operation = "MakePreBooking",
                    IsSuccess = true,
                    CreatedAt = DateTime.UtcNow
                });

                await _bookingRepository.CreateBookingAsync(bookingEntity);
                savedBookingId = bookingEntity.Id;
                Console.WriteLine($">>> DB WRITE SUCCESS BookingId={bookingEntity.Id}");
            }
            catch (Exception dbEx)
            {
                Console.WriteLine($">>> DB WRITE ERROR: {dbEx.Message} | Inner: {dbEx.InnerException?.Message}");
                _logger.LogError(dbEx,
                    "[MakePreBooking] DB kaydi basarisiz. PNR={BookingCode} yine de donuluyor.",
                    result.BookingCode);
            }

            return Ok(new
            {
                result.HasError,
                result.ErrorMessage,
                result.BookingCode,
                result.Status,
                result.TotalFare,
                result.BaseFare,
                result.Taxes,
                result.ServiceFee,
                result.Currency,
                result.ShoppingFileId,
                result.IsPriceChanged,
                result.PrebookingExpiresAt,
                result.ReservationExpiresAt,
                result.Segments,
                result.Passengers,
                bookingId = savedBookingId,
                userId = resolvedUserId,
                guestSessionId = resolvedGuestSessionId,
                isGuest = request.UserId == null
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

    [HttpPost("remove-product")]
    public async Task<IActionResult> RemoveProduct([FromBody] RemoveProductRequest? request)
    {
        try
        {
            if (request == null)
                return BadRequest(new { error = "Request body parse edilemedi. JSON formatini kontrol edin." });

            if (string.IsNullOrWhiteSpace(request.SessionId) || string.IsNullOrWhiteSpace(request.SessionToken))
                return BadRequest(new { error = "SessionId ve SessionToken alanlari zorunludur." });

            if (string.IsNullOrWhiteSpace(request.ProductId))
                return BadRequest(new { error = "ProductId alani zorunludur." });

            var result = await _flightService.RemoveProductAsync(request);
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

    [HttpPost("make-payment")]
    public async Task<IActionResult> MakePayment([FromBody] MakePaymentRequest? request)
    {
        try
        {
            if (request == null)
                return BadRequest(new { error = "Request body parse edilemedi. JSON formatini kontrol edin." });

            if (string.IsNullOrWhiteSpace(request.SessionId) || string.IsNullOrWhiteSpace(request.SessionToken))
                return BadRequest(new { error = "SessionId ve SessionToken alanlari zorunludur." });

            if (string.IsNullOrWhiteSpace(request.ShoppingFileId))
                return BadRequest(new { error = "ShoppingFileId alani zorunludur." });

            if (string.IsNullOrWhiteSpace(request.ProductId))
                return BadRequest(new { error = "ProductId alani zorunludur." });

            if (request.Amount <= 0)
                return BadRequest(new { error = "Amount sifirdan buyuk olmalidir." });

            if (request.PaymentType == "CreditCard" && request.CreditCard == null)
                return BadRequest(new { error = "Kredi karti ile odeme icin CreditCard bilgileri zorunludur." });

            if (request.CreditCard != null)
            {
                if (string.IsNullOrWhiteSpace(request.CreditCard.CardNumber))
                    return BadRequest(new { error = "Kart numarasi zorunludur." });

                if (string.IsNullOrWhiteSpace(request.CreditCard.CardHolderName))
                    return BadRequest(new { error = "Kart sahibi adi zorunludur." });

                if (string.IsNullOrWhiteSpace(request.CreditCard.ExpiryMonth) || string.IsNullOrWhiteSpace(request.CreditCard.ExpiryYear))
                    return BadRequest(new { error = "Son kullanma tarihi (ay/yil) zorunludur." });

                if (string.IsNullOrWhiteSpace(request.CreditCard.Cvv))
                    return BadRequest(new { error = "CVV zorunludur." });
            }

            var result = await _flightService.MakePaymentAsync(request);

            if (result == null)
            {
                return StatusCode(500, new { error = "MakePayment servisten null response dondu." });
            }

            // Odeme basariliysa DB'deki booking durumunu guncelle
            if (!result.HasError && result.IsPaymentSuccessful && request.BookingId.HasValue)
            {
                try
                {
                    await _bookingRepository.UpdateStatusAsync(request.BookingId.Value, "Paid");
                    await _bookingRepository.AddLogAsync(new BookingLog
                    {
                        Id = Guid.NewGuid(),
                        BookingId = request.BookingId.Value,
                        SessionId = request.SessionId ?? "",
                        SessionToken = request.SessionToken ?? "",
                        Operation = "MakePayment",
                        IsSuccess = true,
                        CreatedAt = DateTime.UtcNow
                    });
                }
                catch (Exception dbEx)
                {
                    _logger.LogError(dbEx, "[MakePayment] DB guncelleme basarisiz. BookingId={BookingId}", request.BookingId);
                }
            }

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

    [HttpPost("finalize-shopping")]
    public async Task<IActionResult> FinalizeShopping([FromBody] FinalizeShoppingRequest? request)
    {
        try
        {
            if (request == null)
                return BadRequest(new { error = "Request body parse edilemedi. JSON formatini kontrol edin." });

            if (string.IsNullOrWhiteSpace(request.SessionId) || string.IsNullOrWhiteSpace(request.SessionToken))
                return BadRequest(new { error = "SessionId ve SessionToken alanlari zorunludur." });

            if (string.IsNullOrWhiteSpace(request.ShoppingFileId))
                return BadRequest(new { error = "ShoppingFileId alani zorunludur." });

            if (string.IsNullOrWhiteSpace(request.ProductId))
                return BadRequest(new { error = "ProductId alani zorunludur." });

            var result = await _flightService.FinalizeShoppingAsync(request);

            // Biletleme basariliysa DB'deki booking durumunu guncelle
            if (!result.HasError && request.BookingId.HasValue)
            {
                try
                {
                    var booking = await _bookingRepository.GetByIdAsync(request.BookingId.Value);
                    if (booking != null)
                    {
                        booking.Status = result.Status ?? "Ticketed";
                        booking.IsFinalized = true;
                        booking.TicketedAt = DateTime.UtcNow;
                        booking.UpdatedAt = DateTime.UtcNow;

                        // Bilet numaralarini yolculara ata
                        foreach (var ticket in result.Tickets)
                        {
                            var pax = booking.Passengers.FirstOrDefault(p =>
                                string.Equals(p.FirstName, ticket.FirstName, StringComparison.OrdinalIgnoreCase) &&
                                string.Equals(p.LastName, ticket.LastName, StringComparison.OrdinalIgnoreCase));

                            if (pax != null)
                                pax.TicketNumber = ticket.TicketNumber;
                        }

                        await _bookingRepository.UpdateStatusAsync(request.BookingId.Value, booking.Status);
                        await _bookingRepository.AddLogAsync(new BookingLog
                        {
                            Id = Guid.NewGuid(),
                            BookingId = request.BookingId.Value,
                            SessionId = request.SessionId,
                            SessionToken = request.SessionToken,
                            Operation = "FinalizeShopping",
                            IsSuccess = true,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }
                catch (Exception dbEx)
                {
                    _logger.LogError(dbEx, "[FinalizeShopping] DB guncelleme basarisiz. BookingId={BookingId}", request.BookingId);
                }
            }

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

    [HttpPost("poke-shopping-file")]
    public async Task<IActionResult> PokeShoppingFile([FromBody] PokeShoppingFileRequest? request)
    {
        try
        {
            if (request == null)
                return BadRequest(new { error = "Request body parse edilemedi. JSON formatini kontrol edin." });

            if (string.IsNullOrWhiteSpace(request.SessionId) || string.IsNullOrWhiteSpace(request.SessionToken))
                return BadRequest(new { error = "SessionId ve SessionToken alanlari zorunludur." });

            if (string.IsNullOrWhiteSpace(request.ShoppingFileId))
                return BadRequest(new { error = "ShoppingFileId alani zorunludur." });

            var result = await _flightService.PokeShoppingFileAsync(request);
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

    [HttpPost("read-shopping-file")]
    public async Task<IActionResult> ReadShoppingFile([FromBody] ReadShoppingFileRequest? request)
    {
        try
        {
            if (request == null)
                return BadRequest(new { error = "Request body parse edilemedi. JSON formatini kontrol edin." });

            if (string.IsNullOrWhiteSpace(request.SessionId) || string.IsNullOrWhiteSpace(request.SessionToken))
                return BadRequest(new { error = "SessionId ve SessionToken alanlari zorunludur." });

            if (string.IsNullOrWhiteSpace(request.ShoppingFileId))
                return BadRequest(new { error = "ShoppingFileId alani zorunludur." });

            var result = await _flightService.ReadShoppingFileAsync(request);
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

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest? request)
    {
        try
        {
            if (request == null)
                return BadRequest(new { error = "Request body parse edilemedi. JSON formatini kontrol edin." });

            if (string.IsNullOrWhiteSpace(request.SessionId) || string.IsNullOrWhiteSpace(request.SessionToken))
                return BadRequest(new { error = "SessionId ve SessionToken alanlari zorunludur." });

            var result = await _flightService.LogoutAsync(request);
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

    [HttpGet("booking/{bookingId}")]
    public async Task<IActionResult> GetBooking(Guid bookingId)
    {
        try
        {
            var booking = await _bookingRepository.GetByIdAsync(bookingId);
            if (booking == null)
                return NotFound(new { error = $"Booking bulunamadi: {bookingId}" });

            return Ok(new
            {
                booking.Id,
                booking.PNR,
                booking.Status,
                booking.GrandTotal,
                booking.Currency,
                booking.IsFinalized,
                booking.Origin,
                booking.Destination,
                booking.AirlineCode,
                booking.FlightNumber,
                booking.AdultCount,
                booking.ChildCount,
                booking.InfantCount,
                booking.ServiceFee,
                booking.CreatedAt,
                booking.BookedAt,
                booking.PaidAt,
                booking.TicketedAt,
                booking.SessionId,
                booking.SessionToken,
                isGuest = booking.UserId == null,
                booking.UserId,
                booking.GuestSessionId,
                passengers = booking.Passengers.Select(p => new
                {
                    p.SequenceNo,
                    p.Type,
                    p.FirstName,
                    p.LastName,
                    p.Gender,
                    p.BirthDate,
                    p.CitizenNo,
                    p.PassportNo,
                    p.Nationality,
                    p.TicketNumber,
                    p.Email,
                    p.Phone
                }),
                segments = booking.FlightSegments.Select(s => new
                {
                    s.MarketingAirline,
                    s.FlightNumber,
                    s.OriginCode,
                    s.DestinationCode,
                    s.DepartureDate,
                    s.DepartureTime,
                    s.ArrivalDate,
                    s.ArrivalTime,
                    s.BookingClass
                })
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("booking/pnr/{pnr}")]
    public async Task<IActionResult> GetBookingByPnr(string pnr)
    {
        try
        {
            var booking = await _bookingRepository.GetByPnrAsync(pnr);
            if (booking == null)
                return NotFound(new { error = $"PNR bulunamadi: {pnr}" });

            return Ok(new
            {
                booking.Id,
                booking.PNR,
                booking.Status,
                booking.GrandTotal,
                booking.Currency,
                booking.IsFinalized,
                booking.Origin,
                booking.Destination,
                booking.AirlineCode,
                booking.FlightNumber,
                booking.AdultCount,
                booking.ChildCount,
                booking.InfantCount,
                booking.CreatedAt,
                booking.BookedAt,
                booking.PaidAt,
                booking.TicketedAt,
                isGuest = booking.UserId == null,
                passengers = booking.Passengers.Select(p => new
                {
                    p.SequenceNo, 
                    p.Type,
                    p.FirstName,
                    p.LastName,
                    p.Gender,
                    p.TicketNumber
                }),
                segments = booking.FlightSegments.Select(s => new
                {
                    s.MarketingAirline,
                    s.FlightNumber,
                    s.OriginCode,
                    s.DestinationCode,
                    s.DepartureDate,
                    s.DepartureTime,
                    s.ArrivalDate,
                    s.ArrivalTime
                })
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}