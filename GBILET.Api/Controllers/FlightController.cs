using GBILET.Core.DTOs.Payment;
using GBILET.Core.Entities;
using GBILET.Core.Helpers;
using GBILET.Core.Interfaces;
using GBILET.Core.Models.Flight;
using GBILET.Core.Service;
using GBILET.Core.Service.Flight;
using GBILET.Infrastructure.Resilience;
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
    private readonly IPaymentService _paymentService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<FlightController> _logger;
    private readonly string _frontendUrl;
    private readonly string? _appBaseUrl;
    private readonly FlightAllocateService _flightAllocateService;
    private readonly SessionRecoveryExecutor _sessionRecoveryExecutor;

    public FlightController(
        IFlightService flightService,
        IBookingRepository bookingRepository,
        IPaymentService paymentService,
        IMemoryCache cache,
        ILogger<FlightController> logger,
        IConfiguration configuration,
        FlightAllocateService flightAllocateService,
        SessionRecoveryExecutor sessionRecoveryExecutor)
    {
        _flightService = flightService;
        _bookingRepository = bookingRepository;
        _paymentService = paymentService;
        _cache = cache;
        _logger = logger;
        _frontendUrl = configuration["FrontendUrl"] ?? "http://localhost:3000";
        _appBaseUrl = configuration["AppBaseUrl"];
        _flightAllocateService = flightAllocateService;
        _sessionRecoveryExecutor = sessionRecoveryExecutor;
    }

    private string BuildCallbackUrl() =>
        !string.IsNullOrEmpty(_appBaseUrl)
            ? $"{_appBaseUrl.TrimEnd('/')}/api/Flight/3d-callback"
            : $"{Request.Scheme}://{Request.Host}/api/Flight/3d-callback";





    [HttpPost("search")]
    public async Task<IActionResult> Search([FromBody] SearchRequest request)
    {
        try
        {
            if (request.FlightType == "MP")
            {
                if (request.Segments == null || request.Segments.Count < 2)
                    return BadRequest(new { error = "Çoklu şehir aramasında en az 2 segment gereklidir." });

                if (request.Segments.Count > 6)
                    return BadRequest(new { error = "Çoklu şehir aramasında en fazla 6 segment eklenebilir." });

                foreach (var seg in request.Segments)
                {
                    if (string.IsNullOrWhiteSpace(seg.Origin) || string.IsNullOrWhiteSpace(seg.Destination))
                        return BadRequest(new { error = "Her segmentte Origin ve Destination zorunludur." });

                    if (seg.DepartureDate == default)
                        return BadRequest(new { error = "Her segmentte DepartureDate zorunludur." });
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(request.Origin) || string.IsNullOrWhiteSpace(request.Destination))
                    return BadRequest(new { error = "Origin ve Destination alanları zorunludur." });

                if (request.DepartureDate == default)
                    return BadRequest(new { error = "DepartureDate alanı zorunludur." });

                if (request.FlightType == "RT" && !request.ReturnDate.HasValue)
                    return BadRequest(new { error = "Gidiş-dönüş uçuşlar için ReturnDate zorunludur." });
            }

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
                error = "Internal server error"
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
                error = "Internal server error"
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

            // FlightAllocateService:
            //   - Provider'a HER ZAMAN gider (allocate cache'lenmez)
            //   - Search cache snapshot ile karsilastirma yapip Change bilgisini doldurur
            //   - SessionRecoveryExecutor ile sarmaldir; session expire'da otomatik recovery yapar
            var allocateResult = await _flightAllocateService.AllocateAsync(
                request,
                originalSearchCriteria: request.SearchRequest,
                currency: "TRY");

            var result = allocateResult.Allocate;

            // Session cache'ini allocate sonucu ile guncelle — ShoppingFileId degisebilir
            if (!result.HasError)
            {
                try
                {
                    var searchId = Request.Headers["x-search-id"].FirstOrDefault();
                    if (!string.IsNullOrEmpty(searchId))
                    {
                        var cacheKey = $"flight_session_{searchId}";
                        if (_cache.TryGetValue<FlightSessionData>(cacheKey, out var sessionData) && sessionData != null)
                        {
                            if (!string.IsNullOrEmpty(result.ShoppingFileId))
                                sessionData.ShoppingFileId = result.ShoppingFileId;

                            // Allocate response'tan gelen ProductId'yi sakla
                            var firstProduct = result.AirBookings.FirstOrDefault()?.ProductId;
                            if (!string.IsNullOrEmpty(firstProduct))
                                sessionData.ProductId = firstProduct;

                            _cache.Set(cacheKey, sessionData, new MemoryCacheEntryOptions()
                                .SetAbsoluteExpiration(TimeSpan.FromMinutes(20)));

                            _logger.LogInformation(
                                "[Allocate] Session cache guncellendi: SearchId={SearchId}, ShoppingFileId={ShoppingFileId}, ProductId={ProductId}",
                                searchId, result.ShoppingFileId, firstProduct);
                        }
                    }
                }
                catch (Exception cacheEx)
                {
                    _logger.LogWarning(cacheEx, "[Allocate] Session cache guncellemesi basarisiz.");
                }
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                error = "Internal server error"
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

            // Telefon numarasini normalize et (ornek: 5351234567 → +90-5351234567)
            request.Contact.Phone = NormalizePhoneNumber(request.Contact.Phone);

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

                // Kimlik bilgisi kontrolu — BiletBank CitizenNo veya PassportNo zorunlu tutuyor
                if (string.IsNullOrWhiteSpace(pax.CitizenNo) && string.IsNullOrWhiteSpace(pax.PassportNo))
                    return BadRequest(new { error = $"Yolcu {pax.SequenceNo}: Kimlik bilgisi zorunludur. TC kimlik no veya pasaport numarasi girilmelidir." });

                if (string.IsNullOrWhiteSpace(pax.TempTag))
                    pax.TempTag = pax.PaxReferenceId;

                _logger.LogInformation(
                    "[UpdatePassengers] Pax {SeqNo}: Type={PaxType}, TempTag={TempTag}",
                    pax.SequenceNo, pax.PaxType, pax.TempTag);
            }

            var result = await _flightService.UpdatePassengersAsync(request);
            return Ok(new { result.HasError, result.ErrorMessage, result.RawSoapRequest, result.RawSoapResponse });
        }
        catch (GBILET.Core.Exceptions.BiletBankSessionExpiredException sessionEx)
        {
            // UpdatePassengers icin SessionRecoveryExecutor kullanilmaz: yeni session = yeni shoppingFileId
            // gerektigi icin partial recovery booking state'ini bozardi. Frontend re-search yapmali.
            _logger.LogWarning(sessionEx, "[UpdatePassengers] BiletBank session expired. Frontend should restart from search.");
            return StatusCode(409, new
            {
                error = "Oturum süresi doldu, lütfen aramayı yeniden yapınız.",
                code = "SESSION_EXPIRED",
                requiresResearch = true
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                error = "Internal server error"
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

            // BrandedFareItemId opsiyonel — bazi havayollari (orn. AnadoluJet) branded fare desteklemez

            if (string.IsNullOrWhiteSpace(request.ShoppingFileId))
                return BadRequest(new { error = "ShoppingFileId alani zorunludur (Allocate response'taki ShoppingFileId)." });

            if (request.Passengers == null || request.Passengers.Count == 0)
                return BadRequest(new { error = "En az bir yolcu bilgisi girilmelidir (DB kaydı icin gerekli)." });

            if (request.Contact == null || string.IsNullOrWhiteSpace(request.Contact.Email))
                return BadRequest(new { error = "Iletisim bilgisi (Email) zorunludur." });

            // Telefon numarasini normalize et
            if (request.Contact != null && !string.IsNullOrWhiteSpace(request.Contact.Phone))
                request.Contact.Phone = NormalizePhoneNumber(request.Contact.Phone);

            var result = await _flightService.MakePreBookingAsync(request);

            _logger.LogInformation("[MakePreBooking] Result: HasError={HasError}, BookingCode={BookingCode}, Status={Status}",
                result.HasError, result.BookingCode, result.Status);

            if (result.IsPriceChanged)
            {
                _logger.LogWarning("[MakePreBooking] Price changed detected after MakePreBooking call.");
            }

            if (result.HasError)
                return Ok(result);

            // Session cache'ini fiyat bilgileriyle guncelle — BookingCode bos olsa bile fiyat bilgisi gelir
            try
            {
                var searchId = Request.Headers["x-search-id"].FirstOrDefault();
                if (!string.IsNullOrEmpty(searchId))
                {
                    var cacheKey = $"flight_session_{searchId}";
                    if (_cache.TryGetValue<FlightSessionData>(cacheKey, out var sessionData) && sessionData != null)
                    {
                        if (result.IsPriceChanged)
                        {
                            // Eski fiyati response'a ekle — frontend modal'da gosterecek
                            result.OldPrice = sessionData.GrandTotal;
                            _logger.LogWarning("[MakePreBooking] Fiyat degisti! Eski GrandTotal: {EskiFiyat}, Yeni TotalFare: {YeniFiyat}",
                                sessionData.GrandTotal, result.TotalFare);
                        }

                        sessionData.ProductId = result.ProductId;
                        sessionData.BookingCode = result.BookingCode;
                        sessionData.GrandTotal = result.TotalFare;
                        sessionData.TotalFare = result.TotalFare;
                        sessionData.BaseFare = result.BaseFare;
                        sessionData.Taxes = result.Taxes;
                        sessionData.ServiceFee = result.ServiceFee;
                        sessionData.Currency = result.Currency;
                        sessionData.Status = result.Status;
                        sessionData.ShoppingFileId = result.ShoppingFileId ?? sessionData.ShoppingFileId;

                        _cache.Set(cacheKey, sessionData, new MemoryCacheEntryOptions()
                            .SetAbsoluteExpiration(TimeSpan.FromMinutes(20)));

                        _logger.LogInformation("[MakePreBooking] Session cache guncellendi: SearchId={SearchId}, GrandTotal={GrandTotal}",
                            searchId, result.TotalFare);
                    }
                }
            }
            catch (Exception cacheEx)
            {
                _logger.LogWarning(cacheEx, "[MakePreBooking] Session cache guncellemesi basarisiz.");
            }

            if (string.IsNullOrEmpty(result.BookingCode))
            {
                _logger.LogWarning("[MakePreBooking] Booking code empty, skipping DB write. BookingCode='{BookingCode}'", result.BookingCode);
                return Ok(result);
            }

            // Basarili prebooking — DB'ye booking kaydi olustur
            Guid? resolvedUserId = null;
            Guid? resolvedGuestSessionId = null;
            Guid? savedBookingId = null;

            try
            {
                _logger.LogInformation("[MakePreBooking] DB write start for BookingCode={BookingCode}", result.BookingCode);

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
                    ProductId = result.ProductId ?? request.ProductId,
                    ShoppingFileId = result.ShoppingFileId ?? request.ShoppingFileId,
                    Origin = firstSegment?.OriginCode,
                    Destination = firstSegment?.DestinationCode,
                    AirlineCode = firstSegment?.MarketingAirline,
                    FlightNumber = firstSegment?.FlightNumber,
                    AllocatedAt = DateTime.UtcNow,
                    BookedAt = DateTime.UtcNow,
                    AdultCount = adultCount > 0 ? adultCount : 1,
                    ChildCount = childCount,
                    InfantCount = infantCount,
                    // Biletleme son tarihi: havayolu/GDS bu sureye kadar PNR'i tutar.
                    // Sure dolunca PNR otomatik dusurulur, koltuk yeniden satisa acilir.
                    // Reservation_ExpiresAt yoksa Prebooking_ExpiresAt'e fallback yap.
                    TicketTimeLimit = NormalizeUtc(result.ReservationExpiresAt ?? result.PrebookingExpiresAt)
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
                        SequenceNo = bookingEntity.FlightSegments.Count + 1,
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

                bookingEntity.FareDetails.Add(new FareDetail
                {
                    Id = Guid.NewGuid(),
                    BookingId = bookingEntity.Id,
                    BaseFare = result.BaseFare,
                    TotalTax = result.Taxes,
                    ServiceFee = result.ServiceFee,
                    GrandTotal = result.TotalFare,
                    Currency = result.Currency ?? "TRY",
                    CreatedAt = DateTime.UtcNow
                });

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
                _logger.LogInformation("[MakePreBooking] DB write success. BookingId={BookingId}", bookingEntity.Id);

                // BookingId'yi de session'a yaz
                try
                {
                    var searchIdForBooking = Request.Headers["x-search-id"].FirstOrDefault();
                    if (!string.IsNullOrEmpty(searchIdForBooking))
                    {
                        var bCacheKey = $"flight_session_{searchIdForBooking}";
                        if (_cache.TryGetValue<FlightSessionData>(bCacheKey, out var bSession) && bSession != null)
                        {
                            bSession.BookingId = bookingEntity.Id;
                            _cache.Set(bCacheKey, bSession, new MemoryCacheEntryOptions()
                                .SetAbsoluteExpiration(TimeSpan.FromMinutes(20)));
                        }
                    }
                }
                catch { /* booking id cache hatasi kritik degil */ }
            }
            catch (Exception dbEx)
            {
                _logger.LogError(dbEx,
                    "[MakePreBooking] DB kaydi basarisiz. PNR={BookingCode} yine de donuluyor.",
                    result.BookingCode);
            }

            var responseBody = new
            {
                result.HasError,
                result.ErrorMessage,
                result.BookingCode,
                result.Status,
                result.TotalFare,
                grandTotal = result.TotalFare,
                result.BaseFare,
                result.Taxes,
                result.ServiceFee,
                result.Currency,
                result.ShoppingFileId,
                result.IsPriceChanged,
                result.OldPrice,
                result.PrebookingExpiresAt,
                result.ReservationExpiresAt,
                result.Segments,
                result.Passengers,
                bookingId = savedBookingId,
                userId = resolvedUserId,
                guestSessionId = resolvedGuestSessionId,
                isGuest = request.UserId == null
            };

            // Fiyat degisti ise 409 Conflict don — frontend kullanicidan onay alacak
            if (result.IsPriceChanged)
            {
                _logger.LogWarning(
                    "[MakePreBooking] Fiyat degisikligi tespit edildi. Eski: {OldPrice}, Yeni: {NewPrice}. 409 donuluyor.",
                    result.OldPrice, result.TotalFare);
                return Conflict(new
                {
                    code = "PRICE_CHANGED",
                    message = "Ucus fiyati degismistir. Devam etmek icin yeni fiyati onaylayin.",
                    oldPrice = result.OldPrice,
                    newPrice = result.TotalFare,
                    data = responseBody
                });
            }

            return Ok(responseBody);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                error = "Internal server error"
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

            if (!Guid.TryParse(request.ProductId, out var productGuid) || productGuid == Guid.Empty)
                return BadRequest(new { error = "ProductId gecerli ve bos olmayan bir GUID olmalidir.", receivedValue = request.ProductId });

            if (string.IsNullOrWhiteSpace(request.ShoppingFileId))
                return BadRequest(new { error = "ShoppingFileId alani zorunludur (Allocate response'taki ShoppingFileId)." });

            if (!Guid.TryParse(request.ShoppingFileId, out var shoppingGuid) || shoppingGuid == Guid.Empty)
                return BadRequest(new { error = "ShoppingFileId gecerli ve bos olmayan bir GUID olmalidir.", receivedValue = request.ShoppingFileId });

            var result = await _flightService.RemoveProductAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                error = "Internal server error"
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

            _logger.LogInformation("[MakePayment] Gelen request: Amount={Amount}, PaymentType={PaymentType}, SessionId={SessionId}, ShoppingFileId={ShoppingFileId}, ProductId={ProductId}",
                request.Amount, request.PaymentType, request.SessionId, request.ShoppingFileId, request.ProductId);

            if (string.IsNullOrWhiteSpace(request.SessionId) || string.IsNullOrWhiteSpace(request.SessionToken))
                return BadRequest(new { error = "SessionId ve SessionToken alanlari zorunludur." });

            if (string.IsNullOrWhiteSpace(request.ShoppingFileId))
                return BadRequest(new { error = "ShoppingFileId alani zorunludur." });

            if (request.Amount <= 0)
                return BadRequest(new { error = $"Amount sifirdan buyuk olmalidir. Gelen deger: {request.Amount}" });

            if ((request.PaymentType == "CreditCard" || request.PaymentType == "CreditCardDirect") && request.CreditCard == null)
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

            // Tek kullanımlık nonce üret — 3D callback replay saldırısını önler
            var callbackNonce = Guid.NewGuid().ToString("N");
            var baseCallbackUrl = BuildCallbackUrl();

            // Cache'e yaz — nonce dahil (FinalizeShopping + replay koruması için)
            _cache.Set($"3d_session_{request.ShoppingFileId}", new ThreeDSessionData
            {
                SessionId = request.SessionId,
                SessionToken = request.SessionToken,
                ShoppingFileId = request.ShoppingFileId,
                BookingId = request.BookingId,
                ProductId = request.ProductId,
                BillingInfo = request.BillingInfo,
                Nonce = callbackNonce,
                FrontendBaseUrl = string.IsNullOrWhiteSpace(request.FrontendBaseUrl) ? null : request.FrontendBaseUrl.TrimEnd('/')
            }, TimeSpan.FromMinutes(15));

            // Nonce'u ayrıca doğrulama için kaydet
            _cache.Set($"3d_nonce_{callbackNonce}", request.ShoppingFileId, TimeSpan.FromMinutes(15));

            request.CallbackNonce = callbackNonce;

            var result = await _paymentService.ProcessPaymentAsync(new PaymentProcessRequest
            {
                Payment = request,
                CallbackBaseUrl = baseCallbackUrl
            });

            if (result == null)
            {
                return StatusCode(500, new { error = "PaymentService null response dondu." });
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MakePayment] Controller exception");
            return StatusCode(500, new
            {
                hasError = true,
                errorMessage = "Ödeme işlemi sırasında beklenmeyen bir hata oluştu.",
                error = "Internal server error"
            });
        }
    }

    /// <summary>
    /// 3D Secure tamamlandiktan sonra frontend'in cagirabilecegi JSON API endpoint'i.
    /// Banka callback'i otomatik calismazsa veya SPA'dan manuel tetiklemek icin kullanilir.
    /// 3D callback sonrasi FinalizeShopping yapmak icin session bilgilerine ihtiyac vardir.
    /// </summary>
    [HttpPost("complete-3d-payment")]
    public async Task<IActionResult> Complete3DPayment([FromBody] Complete3DPaymentRequest? request)
    {
        try
        {
            if (request == null)
                return BadRequest(new { error = "Request body parse edilemedi. JSON formatini kontrol edin." });

            if (string.IsNullOrWhiteSpace(request.SessionId) || string.IsNullOrWhiteSpace(request.SessionToken))
                return BadRequest(new { error = "SessionId ve SessionToken alanlari zorunludur." });

            if (string.IsNullOrWhiteSpace(request.ShoppingFileId))
                return BadRequest(new { error = "ShoppingFileId alani zorunludur." });

            // Cache'ten ProductId/BillingInfo'yu cek (varsa) — auto-finalize icin gerekli
            string? productId = null;
            ShoppingBillingInfo? billingInfo = null;
            if (_cache.TryGetValue<ThreeDSessionData>($"3d_session_{request.ShoppingFileId}", out var cached) && cached != null)
            {
                productId = cached.ProductId;
                billingInfo = cached.BillingInfo;
            }

            var result = await _paymentService.ProcessComplete3DAsync(new Complete3DRequest
            {
                Payment = request,
                ProductId = productId,
                BillingInfo = billingInfo
            });

            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                error = "Internal server error"
            });
        }
    }

    /// <summary>
    /// Banka 3D Secure dogrulamasi sonrasi bu endpoint'e POST yapar.
    /// Bankadan gelen form parametreleri (MD, PaRes vb.) BiletBank'a iletilir.
    /// Sonuc ne olursa olsun kullanici frontend'e redirect edilir.
    /// </summary>
    [HttpPost("3d-callback")]
    [HttpGet("3d-callback")]
    public async Task<IActionResult> ThreeDCallback()
    {
        try
        {
            _logger.LogInformation("[3DCallback] Request received. Method={Method}, ContentType={ContentType}",
                Request.Method, Request.ContentType);

            // Gelen tum query string parametrelerini logla
            foreach (var key in Request.Query.Keys)
            {
                _logger.LogInformation("[3DCallback] QueryString param: {Key}={Value}", key, Request.Query[key].ToString());
            }

            // Bankadan gelen tum form parametrelerini topla
            var bankParams = new Dictionary<string, string>();

            if (Request.HasFormContentType)
            {
                var form = await Request.ReadFormAsync();
                foreach (var key in form.Keys)
                {
                    bankParams[key] = form[key].ToString();
                    _logger.LogInformation("[3DCallback] Form param: {Key}={Value}",
                        key, key.Equals("PaRes", StringComparison.OrdinalIgnoreCase) ? "[MASKED]" : form[key].ToString());
                }
            }

            foreach (var key in Request.Query.Keys)
            {
                if (!bankParams.ContainsKey(key) && key != "sid" && key != "stk" && key != "sfid" && key != "bid")
                {
                    bankParams[key] = Request.Query[key].ToString();
                    _logger.LogInformation("[3DCallback] Query param: {Key}={Value}", key, Request.Query[key].ToString());
                }
            }

            if (bankParams.Count == 0)
            {
                _logger.LogWarning("[3DCallback] Bankadan parametre gelmedi.");
            }

            // Session bilgilerini query string'den al
            var sessionId = Request.Query["sid"].ToString();
            var sessionToken = Request.Query["stk"].ToString();
            var shoppingFileId = Request.Query["sfid"].ToString();
            var bookingId = Guid.TryParse(Request.Query["bid"].ToString(), out var bid) ? bid : (Guid?)null;
            var callbackNonce = Request.Query["nonce"].ToString();
            string? productId = null;
            ShoppingBillingInfo? billingInfo = null;

            // Nonce doğrulama — replay saldırısını önler
            if (!string.IsNullOrEmpty(callbackNonce))
            {
                if (!_cache.TryGetValue<string>($"3d_nonce_{callbackNonce}", out _))
                {
                    _logger.LogWarning("[3DCallback] Geçersiz veya zaten kullanılmış nonce. Nonce={Nonce}", callbackNonce);
                    return Redirect($"{_frontendUrl}/checkout/failed?error={Uri.EscapeDataString("Geçersiz ödeme oturumu. Lütfen tekrar deneyin.")}");
                }
                // Nonce'u hemen tüket — tek kullanım
                _cache.Remove($"3d_nonce_{callbackNonce}");
            }

            // Query string'te yoksa cache'ten dene
            string? cachedFrontendUrl = null;
            if (string.IsNullOrEmpty(sessionId) || string.IsNullOrEmpty(sessionToken))
            {
                if (!string.IsNullOrEmpty(shoppingFileId)
                    && _cache.TryGetValue<ThreeDSessionData>($"3d_session_{shoppingFileId}", out var cached)
                    && cached != null)
                {
                    sessionId = cached.SessionId;
                    sessionToken = cached.SessionToken;
                    shoppingFileId = cached.ShoppingFileId;
                    bookingId = cached.BookingId;
                    productId = cached.ProductId;
                    billingInfo = cached.BillingInfo;
                    cachedFrontendUrl = cached.FrontendBaseUrl;
                    _logger.LogInformation("[3DCallback] Session cache'ten alindi. ShoppingFileId={ShoppingFileId}, ProductId={ProductId}", shoppingFileId, productId);
                }
                else
                {
                    _logger.LogError("[3DCallback] Session bilgisi bulunamadi (ne query'de ne cache'te).");
                    return Redirect($"{_frontendUrl}/checkout/failed?error={Uri.EscapeDataString("Session bilgisi bulunamadi. Odeme yeniden baslatilmali.")}");
                }
            }
            else
            {
                // Query string'ten session alindiysa cache'ten ProductId ve BillingInfo'yu al
                if (!string.IsNullOrEmpty(shoppingFileId)
                    && _cache.TryGetValue<ThreeDSessionData>($"3d_session_{shoppingFileId}", out var cached)
                    && cached != null)
                {
                    productId = cached.ProductId;
                    billingInfo = cached.BillingInfo;
                    cachedFrontendUrl = cached.FrontendBaseUrl;
                }
            }

            // BFF'in gönderdiği frontend URL'i kullan, yoksa appsettings'teki fallback
            var effectiveFrontendUrl = string.IsNullOrWhiteSpace(cachedFrontendUrl) ? _frontendUrl : cachedFrontendUrl;

            // BiletBank'a Complete3DPayment + Booking finalize (PaymentService icinde DB transaction)
            var completeRequest = new Complete3DPaymentRequest
            {
                SessionId = sessionId,
                SessionToken = sessionToken,
                ShoppingFileId = shoppingFileId,
                BankResponseParameters = bankParams,
                BookingId = bookingId
            };

            var result = await _paymentService.ProcessComplete3DAsync(new Complete3DRequest
            {
                Payment = completeRequest,
                ProductId = productId,
                BillingInfo = billingInfo
            });

            _logger.LogInformation("[3DCallback] Complete3D result: HasError={HasError}, IsPaymentSuccessful={IsPaymentSuccessful}, Status={Status}, AutoFinalized={AutoFinalized}",
                result.HasError, result.IsPaymentSuccessful, result.Status, result.AutoFinalized);

            if (!result.HasError && result.IsPaymentSuccessful)
            {
                // BB PNR (havayolu/BiletBank kodu) ve InternalPnr (ATA PNR) ayri parametre
                // olarak gonderiliyor. Frontend ATA PNR'i oncelikli gosteriyor.
                var bbPnr = result.PNR ?? "";
                var internalPnr = result.InternalPnr ?? "";
                // Dogrudan /checkout/success'e yonlendiriyoruz (eski /payment/result ara
                // ekrani arada gereksiz bir loading + buyuk tik gosteriyordu).
                var successUrl = $"{effectiveFrontendUrl}/checkout/success?bookingId={bookingId}&pnr={Uri.EscapeDataString(bbPnr)}&internalPnr={Uri.EscapeDataString(internalPnr)}&shoppingFileId={Uri.EscapeDataString(shoppingFileId)}&finalized={result.AutoFinalized}";
                return Redirect(successUrl);
            }
            else
            {
                var errorMsg = result.ErrorMessage ?? "Odeme basarisiz";
                return Redirect($"{effectiveFrontendUrl}/checkout/failed?error={Uri.EscapeDataString(errorMsg)}&bookingId={bookingId}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[3DCallback] Exception");
            return Redirect($"{_frontendUrl}/checkout/failed?error={Uri.EscapeDataString("Ödeme işlemi tamamlanamadı. Lütfen destek ekibiyle iletişime geçin.")}");
        }
    }

    /// <summary>
    /// Odeme alinmis ama biletleme yapilamamis (Paid + IsFinalized=false) booking'leri kurtarma endpoint'i.
    /// Session bilgileri hala gecerliyse FinalizeShopping'i yeniden dener.
    /// Frontend sayfa yenilemesinden sonra veya admin panelden cagrilabilir.
    /// </summary>
    [HttpPost("recover-booking")]
    public async Task<IActionResult> RecoverBooking([FromBody] RecoverBookingRequest? request)
    {
        try
        {
            if (request == null || request.BookingId == Guid.Empty)
                return BadRequest(new { error = "BookingId zorunludur." });

            var booking = await _bookingRepository.GetByIdAsync(request.BookingId);
            if (booking == null)
                return NotFound(new { error = $"Booking bulunamadi: {request.BookingId}" });

            // Zaten biletlenmis mi kontrol et
            if (booking.IsFinalized)
            {
                return Ok(new
                {
                    alreadyFinalized = true,
                    status = booking.Status,
                    pnr = booking.PNR,
                    internalPnr = booking.InternalPnr,
                    message = "Booking zaten biletlenmis."
                });
            }

            // Cari odeme akisindaki gibi: IsFinalized=false olan her booking icin
            // FinalizeShopping'i tekrar denemekte sakinca yok. Status kontrolu agresif olmamali
            // — "Paid" veya "PaymentFailed" disinda bir sey de olabilir (ör. "Pending").
            _logger.LogInformation("[RecoverBooking] Attempting recovery. BookingId={BookingId}, Status={Status}, IsFinalized={IsFinalized}",
                booking.Id, booking.Status, booking.IsFinalized);

            // Session bilgilerini belirle: request'ten gelirse onu kullan, yoksa DB'den al
            var sessionId = !string.IsNullOrEmpty(request.SessionId) ? request.SessionId : booking.SessionId;
            var sessionToken = !string.IsNullOrEmpty(request.SessionToken) ? request.SessionToken : booking.SessionToken;
            var shoppingFileId = !string.IsNullOrEmpty(request.ShoppingFileId)
                ? request.ShoppingFileId
                : booking.BiletBankFileId?.ToString();
            var productId = request.ProductId;

            // Cache'ten ProductId almaya calis
            if (string.IsNullOrEmpty(productId) && !string.IsNullOrEmpty(shoppingFileId)
                && _cache.TryGetValue<ThreeDSessionData>($"3d_session_{shoppingFileId}", out var cached) && cached != null)
            {
                productId = cached.ProductId;
            }

            if (string.IsNullOrEmpty(sessionId) || string.IsNullOrEmpty(sessionToken))
                return BadRequest(new { error = "Session bilgileri (SessionId/SessionToken) bulunamadi. Session suresi dolmus olabilir." });

            if (string.IsNullOrEmpty(shoppingFileId))
                return BadRequest(new { error = "ShoppingFileId bulunamadi." });

            if (string.IsNullOrEmpty(productId))
                return BadRequest(new { error = "ProductId bulunamadi. Cache'teki session suresi dolmus olabilir." });

            _logger.LogInformation("[RecoverBooking] Kurtarma baslatiliyor. BookingId={BookingId}, ProductId={ProductId}",
                request.BookingId, productId);

            var finalizeResult = await _flightService.FinalizeShoppingAsync(new FinalizeShoppingRequest
            {
                SessionId = sessionId,
                SessionToken = sessionToken,
                ShoppingFileId = shoppingFileId,
                ProductId = productId,
                BookingId = request.BookingId,
                BillingInfo = request.BillingInfo
            });

            if (!finalizeResult.HasError)
            {
                try
                {
                    var bookingToUpdate = await _bookingRepository.GetByIdAsync(request.BookingId);
                    if (bookingToUpdate != null)
                    {
                        bookingToUpdate.Status = finalizeResult.Status ?? "Ticketed";
                        bookingToUpdate.IsFinalized = true;
                        bookingToUpdate.TicketedAt = DateTime.UtcNow;
                        bookingToUpdate.UpdatedAt = DateTime.UtcNow;

                        foreach (var ticket in finalizeResult.Tickets)
                        {
                            var pax = bookingToUpdate.Passengers.FirstOrDefault(p =>
                                string.Equals(p.FirstName, ticket.FirstName, StringComparison.OrdinalIgnoreCase) &&
                                string.Equals(p.LastName, ticket.LastName, StringComparison.OrdinalIgnoreCase));
                            if (pax != null)
                                pax.TicketNumber = ticket.TicketNumber;
                        }

                        // Tickets bos donduyse ReadShoppingFile ile tekrar dene (BB Reservation
                        // statusunde bilet numaralari finalize response'unda gelmeyebiliyor).
                        var anyTicket = bookingToUpdate.Passengers.Any(p => !string.IsNullOrEmpty(p.TicketNumber));
                        if (!anyTicket)
                        {
                            try
                            {
                                var read = await _flightService.ReadShoppingFileAsync(new ReadShoppingFileRequest
                                {
                                    SessionId = sessionId,
                                    SessionToken = sessionToken,
                                    ShoppingFileId = shoppingFileId
                                });
                                if (!read.HasError)
                                {
                                    foreach (var t in read.Tickets ?? new())
                                    {
                                        if (string.IsNullOrEmpty(t.TicketNumber)) continue;
                                        var pax = bookingToUpdate.Passengers.FirstOrDefault(p =>
                                            string.Equals(p.FirstName, t.FirstName, StringComparison.OrdinalIgnoreCase) &&
                                            string.Equals(p.LastName, t.LastName, StringComparison.OrdinalIgnoreCase));
                                        if (pax != null && string.IsNullOrEmpty(pax.TicketNumber))
                                            pax.TicketNumber = t.TicketNumber;
                                    }
                                    foreach (var rp in read.Passengers ?? new())
                                    {
                                        if (string.IsNullOrEmpty(rp.TicketNumber)) continue;
                                        var pax = bookingToUpdate.Passengers.FirstOrDefault(p =>
                                            string.Equals(p.FirstName, rp.FirstName, StringComparison.OrdinalIgnoreCase) &&
                                            string.Equals(p.LastName, rp.LastName, StringComparison.OrdinalIgnoreCase));
                                        if (pax != null && string.IsNullOrEmpty(pax.TicketNumber))
                                            pax.TicketNumber = rp.TicketNumber;
                                    }
                                    if ((finalizeResult.Tickets == null || finalizeResult.Tickets.Count == 0) && read.Tickets?.Count > 0)
                                        finalizeResult.Tickets = read.Tickets;
                                }
                            }
                            catch (Exception readEx)
                            {
                                _logger.LogWarning(readEx, "[RecoverBooking] ReadShoppingFile (ticket fallback) basarisiz. BookingId={BookingId}", request.BookingId);
                            }
                        }

                        if (string.IsNullOrEmpty(bookingToUpdate.InternalPnr))
                        {
                            var internalPnr = await PnrGenerator.GenerateUniqueAsync(_bookingRepository);
                            bookingToUpdate.InternalPnr = internalPnr;
                            await _bookingRepository.UpdateInternalPnrAsync(request.BookingId, internalPnr);
                            finalizeResult.InternalPnr = internalPnr;
                        }
                        else
                        {
                            finalizeResult.InternalPnr = bookingToUpdate.InternalPnr;
                        }

                        if (!string.IsNullOrEmpty(finalizeResult.BookingCode))
                            await _bookingRepository.UpdatePnrAsync(request.BookingId, finalizeResult.BookingCode);

                        await _bookingRepository.UpdateStatusAsync(request.BookingId, bookingToUpdate.Status);

                        // Admin panelde "Beklemede" gozuken Pending3D Payment kaydini Success'e cek.
                        await _paymentService.MarkLatestPendingPaymentSuccessAsync(request.BookingId, null);

                        await _bookingRepository.AddLogAsync(new BookingLog
                        {
                            Id = Guid.NewGuid(),
                            BookingId = request.BookingId,
                            SessionId = sessionId,
                            SessionToken = sessionToken,
                            Operation = "FinalizeShopping_Recovery",
                            IsSuccess = true,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }
                catch (Exception dbEx)
                {
                    _logger.LogError(dbEx, "[RecoverBooking] DB guncelleme basarisiz. BookingId={BookingId}", request.BookingId);
                }

                return Ok(new
                {
                    recovered = true,
                    status = finalizeResult.Status,
                    pnr = finalizeResult.BookingCode,
                    internalPnr = finalizeResult.InternalPnr,
                    tickets = finalizeResult.Tickets
                });
            }
            else
            {
                await _bookingRepository.AddLogAsync(new BookingLog
                {
                    Id = Guid.NewGuid(),
                    BookingId = request.BookingId,
                    SessionId = sessionId,
                    SessionToken = sessionToken,
                    Operation = "FinalizeShopping_Recovery",
                    IsSuccess = false,
                    ErrorMessage = finalizeResult.ErrorMessage,
                    CreatedAt = DateTime.UtcNow
                });

                return Ok(new
                {
                    recovered = false,
                    error = finalizeResult.ErrorMessage
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RecoverBooking] Exception. BookingId={BookingId}", request?.BookingId);
            return StatusCode(500, new { error = "Internal server error" });
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
            // BiletBank test ortaminda "Booking" statusu basarili biletlemeyi gosterir.
            // Canli ortamda "Ticketed" donecektir. Her iki durumu da basarili kabul ediyoruz.
            var successStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "Booking", "Ticketed", "Reservation" };
            var isFinalized = !result.HasError
                && !string.IsNullOrEmpty(result.Status)
                && successStatuses.Contains(result.Status);

            if (isFinalized && request.BookingId.HasValue)
            {
                try
                {
                    var booking = await _bookingRepository.GetByIdAsync(request.BookingId.Value);
                    if (booking != null)
                    {
                        booking.Status = result.Status ?? "Booking";
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

                        // InternalPnr uret ve kaydet
                        var internalPnr = await PnrGenerator.GenerateUniqueAsync(_bookingRepository);
                        booking.InternalPnr = internalPnr;
                        await _bookingRepository.UpdateInternalPnrAsync(request.BookingId.Value, internalPnr);
                        result.InternalPnr = internalPnr;

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
                error = "Internal server error"
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
                error = "Internal server error"
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
                error = "Internal server error"
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
                error = "Internal server error"
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

            // Self-heal: Odenmis veya biletlenmis booking'lerde InternalPnr (ATA PNR) eksikse
            // burada uretip kaydet. Boylece frontend her zaman ATA PNR'i alabilir.
            if (string.IsNullOrEmpty(booking.InternalPnr) &&
                (string.Equals(booking.Status, "Paid", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(booking.Status, "Ticketed", StringComparison.OrdinalIgnoreCase) ||
                 booking.IsFinalized))
            {
                try
                {
                    var newInternalPnr = await PnrGenerator.GenerateUniqueAsync(_bookingRepository);
                    await _bookingRepository.UpdateInternalPnrAsync(bookingId, newInternalPnr);
                    booking.InternalPnr = newInternalPnr;
                    _logger.LogInformation("[GetBooking] Eksik InternalPnr otomatik uretildi. BookingId={BookingId}, InternalPnr={InternalPnr}",
                        bookingId, newInternalPnr);
                }
                catch (Exception pnrEx)
                {
                    _logger.LogWarning(pnrEx, "[GetBooking] InternalPnr self-heal basarisiz. BookingId={BookingId}", bookingId);
                }
            }

            return Ok(new
            {
                booking.Id,
                booking.PNR,
                booking.InternalPnr,
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
                booking.TicketTimeLimit,
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
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// 3D Secure ödeme başarısız olduğunda veya iptal edildiğinde rezervasyonun
    /// hâlâ geçerli olup olmadığını kontrol eder. Frontend bu endpoint'i çağırıp
    /// "rezervasyonunuz X dakika daha geçerli, tekrar deneyin" mesajını gösterir
    /// ve aynı booking üzerinden yeni ödeme denemesi sunar.
    /// </summary>
    [HttpGet("booking/{bookingId}/payment-status")]
    public async Task<IActionResult> GetBookingPaymentStatus(Guid bookingId)
    {
        try
        {
            var booking = await _bookingRepository.GetByIdAsync(bookingId);
            if (booking == null)
                return NotFound(new { error = $"Rezervasyon bulunamadi: {bookingId}" });

            var nowUtc = DateTime.UtcNow;
            var tktl = NormalizeUtc(booking.TicketTimeLimit);

            // Rezervasyon süresinin durumu
            bool isExpired = tktl.HasValue && tktl.Value <= nowUtc;
            int? remainingMinutes = tktl.HasValue
                ? Math.Max(0, (int)(tktl.Value - nowUtc).TotalMinutes)
                : (int?)null;

            // Tekrar ödeme yapilabilir mi?
            // Sadece henüz ödenmemiş ve süresi dolmamış rezervasyonlar retry edilebilir.
            var statusLower = booking.Status?.ToLowerInvariant() ?? "";
            bool isAlreadyPaid = statusLower is "paid" or "ticketed" || booking.IsFinalized;
            bool isCancelled = statusLower is "cancelled" or "canceled" || booking.CancelledAt.HasValue;
            const int MaxAttempts = 3;
            bool maxAttemptsReached = booking.PaymentAttemptCount >= MaxAttempts;
            bool canRetry = !isAlreadyPaid && !isCancelled && !isExpired && !maxAttemptsReached
                            && !string.IsNullOrEmpty(booking.SessionId)
                            && !string.IsNullOrEmpty(booking.SessionToken)
                            && !string.IsNullOrEmpty(booking.ShoppingFileId);

            return Ok(new
            {
                bookingId = booking.Id,
                pnr = booking.PNR,
                internalPnr = booking.InternalPnr,
                status = booking.Status,
                grandTotal = booking.GrandTotal,
                currency = booking.Currency,
                origin = booking.Origin,
                destination = booking.Destination,
                airlineCode = booking.AirlineCode,
                flightNumber = booking.FlightNumber,
                lastError = booking.LastError,
                ticketTimeLimit = tktl,
                isExpired,
                remainingMinutes,
                isAlreadyPaid,
                isCancelled,
                canRetry,
                paymentAttemptCount = booking.PaymentAttemptCount,
                maxAttempts = MaxAttempts,
                maxAttemptsReached,
                serverTime = nowUtc
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GetBookingPaymentStatus] Error. BookingId={BookingId}", bookingId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Var olan bir rezervasyon için ödeme tekrar denenir. 3D Secure başarısız olduğunda
    /// veya iptal edildiğinde kullanılır. Yeni allocate/prebooking yapılmaz —
    /// booking'de saklanan SessionId/SessionToken/ShoppingFileId/ProductId tekrar kullanılır,
    /// sadece kart bilgisi yenilenir.
    /// </summary>
    [HttpPost("booking/{bookingId}/retry-payment")]
    public async Task<IActionResult> RetryPayment(Guid bookingId, [FromBody] RetryPaymentRequest? request)
    {
        try
        {
            if (request == null)
                return BadRequest(new { error = "Request body parse edilemedi." });

            if (request.CreditCard == null || string.IsNullOrWhiteSpace(request.CreditCard.CardNumber))
                return BadRequest(new { error = "Kart bilgileri eksik." });

            var booking = await _bookingRepository.GetByIdAsync(bookingId);
            if (booking == null)
                return NotFound(new { error = "Rezervasyon bulunamadı." });

            var nowUtc = DateTime.UtcNow;
            var tktl = NormalizeUtc(booking.TicketTimeLimit);

            // Validasyonlar — backend tarafı son savunma hattı
            var statusLower = booking.Status?.ToLowerInvariant() ?? "";
            if (statusLower is "paid" or "ticketed" || booking.IsFinalized)
                return Conflict(new { error = "Bu rezervasyonun ödemesi zaten tamamlanmış." });

            if (statusLower is "cancelled" or "canceled" || booking.CancelledAt.HasValue)
                return Conflict(new { error = "Bu rezervasyon iptal edilmiş, tekrar ödeme yapılamaz." });

            if (tktl.HasValue && tktl.Value <= nowUtc)
                return Conflict(new { error = "Rezervasyon süresi dolmuş. Lütfen yeni bir arama yapın." });

            const int MaxAttempts = 3;
            if (booking.PaymentAttemptCount >= MaxAttempts)
                return Conflict(new { error = "Maksimum ödeme deneme sayısına ulaşıldı. Lütfen müşteri hizmetlerimizle iletişime geçin." });

            if (string.IsNullOrEmpty(booking.SessionId) ||
                string.IsNullOrEmpty(booking.SessionToken) ||
                string.IsNullOrEmpty(booking.ShoppingFileId))
                return Conflict(new { error = "Rezervasyon oturum bilgisi eksik. Lütfen yeni bir arama yapın." });

            if (booking.GrandTotal == null || booking.GrandTotal <= 0)
                return Conflict(new { error = "Rezervasyon tutarı bulunamadı." });

            // Cache'e ProductId/BillingInfo'yu yaz (3D callback FinalizeShopping için)
            _cache.Set($"3d_session_{booking.ShoppingFileId}", new ThreeDSessionData
            {
                SessionId = booking.SessionId!,
                SessionToken = booking.SessionToken!,
                ShoppingFileId = booking.ShoppingFileId!,
                BookingId = booking.Id,
                ProductId = booking.ProductId,
                BillingInfo = request.BillingInfo
            }, TimeSpan.FromMinutes(15));

            var baseCallbackUrl = BuildCallbackUrl();

            var paymentRequest = new MakePaymentRequest
            {
                SessionId = booking.SessionId!,
                SessionToken = booking.SessionToken!,
                ShoppingFileId = booking.ShoppingFileId!,
                ProductId = booking.ProductId,
                Amount = booking.GrandTotal!.Value,
                Currency = booking.Currency ?? "TRY",
                PaymentType = "CreditCard",
                CreditCard = request.CreditCard,
                InstallmentOptionId = request.InstallmentOptionId,
                BookingId = booking.Id,
                BillingInfo = request.BillingInfo,
                IsPartialPayment = false,
                DeductLastSellerCommission = false
            };

            _logger.LogInformation(
                "[RetryPayment] BookingId={BookingId}, PNR={PNR}, AttemptCount={AttemptCount}, Amount={Amount}",
                booking.Id, booking.PNR, booking.PaymentAttemptCount, paymentRequest.Amount);

            var result = await _paymentService.ProcessPaymentAsync(new PaymentProcessRequest
            {
                Payment = paymentRequest,
                CallbackBaseUrl = baseCallbackUrl
            });

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RetryPayment] Error. BookingId={BookingId}", bookingId);
            return StatusCode(500, new { error = "Internal server error" });
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
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpGet("booking/lookup/{pnr}/{lastName}")]
    public async Task<IActionResult> LookupBookingByPnrAndLastName(string pnr, string lastName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(pnr) || string.IsNullOrWhiteSpace(lastName))
                return BadRequest(new { error = "PNR ve soyad alanlari zorunludur." });

            var booking = await _bookingRepository.GetByInternalPnrAndLastNameAsync(pnr.Trim().ToUpper(), lastName.Trim().ToUpper());
            if (booking == null)
                return NotFound(new { error = "Girilen PNR ve soyad ile eslesen rezervasyon bulunamadi." });

            return Ok(new
            {
                booking.Id,
                PNR = booking.InternalPnr,
                BiletBankPnr = booking.PNR,
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
                }),
                fareDetails = booking.FareDetails.Select(f => new
                {
                    f.BaseFare,
                    f.TotalTax,
                    f.ServiceFee,
                    f.GrandTotal,
                    f.Currency
                })
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpPost("cancel-booking")]
    public async Task<IActionResult> CancelBooking([FromBody] CancelBookingRequest? request)
    {
        try
        {
            if (request == null)
                return BadRequest(new { error = "Request body parse edilemedi." });

            if (string.IsNullOrWhiteSpace(request.SessionId) || string.IsNullOrWhiteSpace(request.SessionToken))
                return BadRequest(new { error = "SessionId ve SessionToken zorunludur." });

            if (string.IsNullOrWhiteSpace(request.ProductId))
                return BadRequest(new { error = "ProductId zorunludur." });

            if (string.IsNullOrWhiteSpace(request.ShoppingFileId))
                return BadRequest(new { error = "ShoppingFileId zorunludur." });

            // BiletBank'ta ürünü kaldır
            var removeResult = await _flightService.RemoveProductAsync(new RemoveProductRequest
            {
                SessionId = request.SessionId,
                SessionToken = request.SessionToken,
                ProductId = request.ProductId,
                ShoppingFileId = request.ShoppingFileId
            });

            // DB'deki booking durumunu güncelle
            if (!removeResult.HasError && request.BookingId.HasValue)
            {
                try
                {
                    var booking = await _bookingRepository.GetByIdAsync(request.BookingId.Value);
                    if (booking != null)
                    {
                        booking.Status = "Cancelled";
                        booking.CancelledAt = DateTime.UtcNow;
                        booking.UpdatedAt = DateTime.UtcNow;
                        await _bookingRepository.UpdateStatusAsync(request.BookingId.Value, "Cancelled");
                    }

                    await _bookingRepository.AddLogAsync(new BookingLog
                    {
                        Id = Guid.NewGuid(),
                        BookingId = request.BookingId.Value,
                        SessionId = request.SessionId ?? "",
                        SessionToken = request.SessionToken ?? "",
                        Operation = "CancelBooking",
                        IsSuccess = !removeResult.HasError,
                        CreatedAt = DateTime.UtcNow
                    });
                }
                catch (Exception dbEx)
                {
                    _logger.LogError(dbEx, "[CancelBooking] DB guncelleme basarisiz. BookingId={BookingId}", request.BookingId);
                }
            }

            return Ok(new
            {
                removeResult.HasError,
                removeResult.ErrorMessage,
                status = removeResult.HasError ? "CancelFailed" : "Cancelled",
                request.BookingId
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpGet("my-bookings/user/{userId}")]
    public async Task<IActionResult> GetBookingsByUser(Guid userId)
    {
        try
        {
            var bookings = await _bookingRepository.GetByUserIdAsync(userId);
            return Ok(bookings.Select(MapBookingSummary));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpGet("my-bookings/guest/{guestSessionId}")]
    public async Task<IActionResult> GetBookingsByGuest(Guid guestSessionId)
    {
        try
        {
            var bookings = await _bookingRepository.GetByGuestSessionIdAsync(guestSessionId);
            return Ok(bookings.Select(MapBookingSummary));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpGet("my-bookings/email/{email}")]
    public async Task<IActionResult> GetBookingsByEmail(string email)
    {
        try
        {
            var guest = await _bookingRepository.GetGuestSessionByEmailAsync(email);
            if (guest == null)
                return Ok(Array.Empty<object>());

            var bookings = await _bookingRepository.GetByGuestSessionIdAsync(guest.Id);
            return Ok(bookings.Select(MapBookingSummary));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpPost("booking-status")]
    public async Task<IActionResult> GetBookingStatus([FromBody] BookingStatusRequest? request)
    {
        try
        {
            if (request == null)
                return BadRequest(new { error = "Request body parse edilemedi." });

            // DB'den booking bilgisi
            Booking? booking = null;
            if (request.BookingId.HasValue)
                booking = await _bookingRepository.GetByIdAsync(request.BookingId.Value);
            else if (!string.IsNullOrWhiteSpace(request.PNR))
                booking = await _bookingRepository.GetByPnrAsync(request.PNR);

            if (booking == null)
                return NotFound(new { error = "Booking bulunamadi." });

            // BiletBank'tan güncel durum (opsiyonel)
            PokeShoppingFileResponse? liveStatus = null;
            if (!string.IsNullOrWhiteSpace(request.SessionId) &&
                !string.IsNullOrWhiteSpace(request.SessionToken) &&
                booking.BiletBankFileId.HasValue)
            {
                try
                {
                    liveStatus = await _flightService.PokeShoppingFileAsync(new PokeShoppingFileRequest
                    {
                        SessionId = request.SessionId,
                        SessionToken = request.SessionToken,
                        ShoppingFileId = booking.BiletBankFileId.Value.ToString()
                    });
                }
                catch (Exception pokeEx)
                {
                    _logger.LogWarning(pokeEx, "[BookingStatus] PokeShoppingFile basarisiz, sadece DB durumu donuluyor.");
                }
            }

            return Ok(new
            {
                booking.Id,
                booking.PNR,
                dbStatus = booking.Status,
                liveStatus = liveStatus?.Status,
                booking.GrandTotal,
                booking.Currency,
                booking.IsFinalized,
                booking.PaidAt,
                booking.TicketedAt,
                booking.CancelledAt,
                isReservationCancelled = liveStatus?.IsReservationCancelled ?? false,
                isPriceChanged = liveStatus?.IsPriceChanged ?? false,
                remainingSum = liveStatus?.RemainingSum ?? 0,
                passengers = booking.Passengers.Select(p => new
                {
                    p.FirstName,
                    p.LastName,
                    p.Type,
                    p.TicketNumber
                })
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    private static object MapBookingSummary(Booking b) => new
    {
        b.Id,
        b.PNR,
        b.InternalPnr,
        b.BiletBankFileId,
        b.Status,
        b.GrandTotal,
        b.Currency,
        b.Origin,
        b.Destination,
        b.AirlineCode,
        b.FlightNumber,
        b.IsFinalized,
        b.AdultCount,
        b.ChildCount,
        b.InfantCount,
        b.CreatedAt,
        b.BookedAt,
        b.PaidAt,
        b.TicketedAt,
        b.CancelledAt,
        b.TicketTimeLimit,
        isGuest = b.UserId == null,
        passengerCount = b.Passengers.Count,
        firstPassenger = b.Passengers.OrderBy(p => p.SequenceNo).Select(p => new
        {
            p.FirstName,
            p.LastName,
            p.TicketNumber
        }).FirstOrDefault(),
        segments = b.FlightSegments.Select(s => new
        {
            s.MarketingAirline,
            s.FlightNumber,
            s.OriginCode,
            s.DestinationCode,
            s.DepartureDate,
            s.DepartureTime
        })
    };

    /// <summary>
    /// PostgreSQL DateTimeKind=Unspecified hatasini onlemek icin DateTime'i UTC'ye normalize eder.
    /// </summary>
    private static DateTime? NormalizeUtc(DateTime? value)
    {
        if (!value.HasValue) return null;
        var v = value.Value;
        return v.Kind switch
        {
            DateTimeKind.Utc => v,
            DateTimeKind.Local => v.ToUniversalTime(),
            _ => DateTime.SpecifyKind(v, DateTimeKind.Utc)
        };
    }

    /// <summary>
    /// Tek request ile uçuş arama, seçim, yolcu bilgisi, ön rezervasyon, ödeme ve biletleme.
    /// Tüm adımları otomatik sırayla çalıştırır — elle kopyala/yapıştır gerekmez.
    /// </summary>
    [HttpPost("book-flight")]
    public async Task<IActionResult> BookFlight([FromBody] BookFlightRequest? request)
    {
        var response = new BookFlightResponse();

        try
        {
            // ── Validasyon ──
            if (request == null)
                return BadRequest(new { error = "Request body parse edilemedi." });

            if (string.IsNullOrWhiteSpace(request.Origin) || string.IsNullOrWhiteSpace(request.Destination))
                return BadRequest(new { error = "Origin ve Destination zorunludur." });

            if (request.DepartureDate == default)
                return BadRequest(new { error = "DepartureDate zorunludur." });

            if (request.Passengers == null || request.Passengers.Count == 0)
                return BadRequest(new { error = "En az bir yolcu bilgisi zorunludur." });

            if (string.IsNullOrWhiteSpace(request.ContactEmail) || string.IsNullOrWhiteSpace(request.ContactPhone))
                return BadRequest(new { error = "ContactEmail ve ContactPhone zorunludur." });

            // Telefon numarasini normalize et
            request.ContactPhone = NormalizePhoneNumber(request.ContactPhone);

            if (request.PaymentType == "CreditCard" && request.CreditCard == null && request.AutoPayAndFinalize)
                return BadRequest(new { error = "Kredi kartı ile ödeme için CreditCard bilgileri zorunludur." });

            // ── ADIM 1: Search ──
            response.Steps.Add("Search başlatılıyor...");

            var searchRequest = new SearchRequest
            {
                Origin = request.Origin,
                Destination = request.Destination,
                DepartureDate = request.DepartureDate,
                ReturnDate = request.ReturnDate,
                FlightType = request.FlightType,
                FlightClass = request.FlightClass,
                AdultCount = request.AdultCount,
                ChildCount = request.ChildCount,
                InfantCount = request.InfantCount,
                SearchReason = "SearchAndBook"
            };

            var searchResult = await _flightService.SearchFlightDtoAsync(searchRequest);
            if (searchResult.HasError || searchResult.Flights.Count == 0)
            {
                response.HasError = true;
                response.ErrorMessage = searchResult.ErrorMessage ?? "Uçuş bulunamadı.";
                response.CompletedStep = "Search";
                return Ok(response);
            }

            response.SessionId = searchResult.SessionId;
            response.SessionToken = searchResult.SessionToken;
            response.TotalFlightsFound = searchResult.Flights.Count;
            response.CompletedStep = "Search";
            response.Steps.Add($"Search tamamlandı. {searchResult.Flights.Count} uçuş bulundu.");

            // Uçuş seçimi (index'e göre, varsayılan 0 = en ucuz)
            var flightIndex = Math.Clamp(request.FlightIndex, 0, searchResult.Flights.Count - 1);
            var selectedFlight = searchResult.Flights[flightIndex];
            var productId = selectedFlight.ProductId!;

            response.Steps.Add($"Uçuş seçildi: {selectedFlight.AirlineCode} {selectedFlight.FlightNumber} — {selectedFlight.TotalFareFormatted}");

            // Branded fare seçimi
            string? selectedBrandedFareItemId = null;
            if (selectedFlight.FarePackages.Count > 0)
            {
                var brandIndex = Math.Clamp(request.BrandedFareIndex, 0, selectedFlight.FarePackages.Count - 1);
                selectedBrandedFareItemId = selectedFlight.FarePackages[brandIndex].BrandedFareItemId;
                response.Steps.Add($"Branded fare seçildi: index={brandIndex}, id={selectedBrandedFareItemId}");
            }

            // ── ADIM 2: Allocate ──
            response.Steps.Add("Allocate başlatılıyor...");

            var allocateRequest = new AllocateRequest
            {
                SessionId = searchResult.SessionId,
                SessionToken = searchResult.SessionToken,
                ProductId = productId,
                BrandedFareItemId = selectedBrandedFareItemId,
                SelectedServiceFee = 0
            };

            var allocateResult = await _flightService.AllocateFlightAsync(allocateRequest);
            if (allocateResult.HasError)
            {
                response.HasError = true;
                response.ErrorMessage = $"Allocate hatası: {allocateResult.ErrorMessage}";
                response.CompletedStep = "Allocate";
                return Ok(response);
            }

            response.ShoppingFileId = allocateResult.ShoppingFileId;
            response.SessionId = allocateResult.SessionId;
            response.SessionToken = allocateResult.SessionToken;
            response.CompletedStep = "Allocate";

            var airBooking = allocateResult.AirBookings.FirstOrDefault();
            var bookingItem = airBooking?.BookingItems.FirstOrDefault();
            response.ProductId = airBooking?.ProductId;
            response.ProductItemId = bookingItem?.ProductItemId;

            // Allocate'ten gelen branded fare item id'yi kullan
            // SelectedBrandedFareItemId segmentte set edilir — allocate edilen GERCEK fare'i temsil eder
            var segmentBrandedId = airBooking?.Segments.FirstOrDefault()?.SelectedBrandedFareItemId;
            var fallbackBrandedId = airBooking?.BrandedFareItems.FirstOrDefault()?.BrandedFareItemId;
            response.BrandedFareItemId = segmentBrandedId ?? fallbackBrandedId ?? selectedBrandedFareItemId;

            response.Steps.Add($"BrandedFareItemId kaynak: segment={segmentBrandedId}, fareItems[0]={fallbackBrandedId}, search={selectedBrandedFareItemId} => kullanilan={response.BrandedFareItemId}");

            response.TotalFare = airBooking?.TotalFare ?? 0;
            response.BaseFare = airBooking?.BaseFare ?? 0;
            response.Taxes = airBooking?.Taxes ?? 0;
            response.ServiceFee = airBooking?.ServiceFee ?? 0;
            response.Currency = airBooking?.Currency ?? "TRY";

            response.Steps.Add($"Allocate tamamlandı. ShoppingFileId={allocateResult.ShoppingFileId}, TotalFare={response.TotalFare}");

            // ── ADIM 3: UpdatePassengers ──
            response.Steps.Add("Yolcu bilgileri güncelleniyor...");

            // Yolcuları allocate response'taki sıraya eşle
            var passengersForUpdate = new List<UpdatePassengerItem>();
            for (int i = 0; i < request.Passengers.Count; i++)
            {
                var pax = request.Passengers[i];
                var allocPax = i < allocateResult.Passengers.Count ? allocateResult.Passengers[i] : null;

                passengersForUpdate.Add(new UpdatePassengerItem
                {
                    PaxType = pax.PaxType,
                    SequenceNo = allocPax?.SequenceNo ?? (i + 1),
                    FirstName = pax.FirstName,
                    LastName = pax.LastName,
                    Gender = pax.Gender,
                    BirthDate = pax.BirthDate,
                    CitizenNo = pax.CitizenNo,
                    PassportNo = pax.PassportNo,
                    PassportCountry = pax.PassportCountry,
                    Nationality = pax.Nationality,
                    PaxReferenceId = allocPax?.PaxReferenceId,
                    TempTag = allocPax?.TempTag ?? allocPax?.PaxReferenceId
                });
            }

            var updateRequest = new UpdatePassengersRequest
            {
                SessionId = allocateResult.SessionId!,
                SessionToken = allocateResult.SessionToken!,
                ShoppingFileId = allocateResult.ShoppingFileId!,
                ProductId = airBooking?.ProductId!,
                ProductItemId = bookingItem?.ProductItemId!,
                Passengers = passengersForUpdate,
                Contact = new UpdatePassengerContact
                {
                    Email = request.ContactEmail,
                    Phone = request.ContactPhone
                }
            };

            var updateResult = await _flightService.UpdatePassengersAsync(updateRequest);
            if (updateResult.HasError)
            {
                response.HasError = true;
                response.ErrorMessage = $"UpdatePassengers hatası: {updateResult.ErrorMessage}";
                response.CompletedStep = "UpdatePassengers";
                return Ok(response);
            }

            response.CompletedStep = "UpdatePassengers";
            response.Steps.Add("Yolcu bilgileri güncellendi.");

            // ── ADIM 4: MakePreBooking ──
            response.Steps.Add("Ön rezervasyon yapılıyor...");

            var preBookingRequest = new MakePreBookingRequest
            {
                SessionId = allocateResult.SessionId!,
                SessionToken = allocateResult.SessionToken!,
                ProductId = airBooking?.ProductId!,
                BrandedFareItemId = response.BrandedFareItemId!,
                ShoppingFileId = allocateResult.ShoppingFileId!,
                UserId = request.UserId,
                Passengers = passengersForUpdate,
                Contact = new UpdatePassengerContact
                {
                    Email = request.ContactEmail,
                    Phone = request.ContactPhone
                }
            };

            var preBookResult = await _flightService.MakePreBookingAsync(preBookingRequest);
            if (preBookResult.HasError)
            {
                response.HasError = true;
                response.ErrorMessage = $"PreBooking hatası: {preBookResult.ErrorMessage}";
                response.CompletedStep = "PreBooking";
                return Ok(response);
            }

            response.PNR = preBookResult.BookingCode;
            response.Status = preBookResult.Status;
            response.TotalFare = preBookResult.TotalFare;
            response.BaseFare = preBookResult.BaseFare;
            response.Taxes = preBookResult.Taxes;
            response.ServiceFee = preBookResult.ServiceFee;
            response.Currency = preBookResult.Currency;
            response.ShoppingFileId = preBookResult.ShoppingFileId;
            response.PrebookingExpiresAt = preBookResult.PrebookingExpiresAt;
            response.ReservationExpiresAt = preBookResult.ReservationExpiresAt;
            response.CompletedStep = "PreBooking";

            var firstSeg = preBookResult.Segments.FirstOrDefault();
            response.FlightNumber = firstSeg?.FlightNumber;
            response.MarketingAirline = firstSeg?.MarketingAirline;
            response.Origin = firstSeg?.OriginCode;
            response.Destination = firstSeg?.DestinationCode;
            response.DepartureDay = firstSeg?.DepartureDay;
            response.DepartureTime = firstSeg?.DepartureTime;

            response.Steps.Add($"Ön rezervasyon tamamlandı. PNR={preBookResult.BookingCode}, TotalFare={preBookResult.TotalFare}");

            // DB'ye booking kaydet (mevcut MakePreBooking logic'ini tekrar kullan)
            Guid? savedBookingId = null;
            try
            {
                Guid? resolvedUserId = null;
                Guid? resolvedGuestSessionId = null;

                if (request.UserId.HasValue && request.UserId.Value != Guid.Empty)
                {
                    resolvedUserId = request.UserId.Value;
                }
                else
                {
                    var existingGuest = await _bookingRepository.GetGuestSessionByEmailAsync(request.ContactEmail);
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
                            Email = request.ContactEmail,
                            Phone = request.ContactPhone,
                            IpAddress = ipAddress,
                            CreatedAt = DateTime.UtcNow
                        });
                        resolvedGuestSessionId = newGuest.Id;
                    }
                }

                int adtCount = request.Passengers.Count(p => p.PaxType == "ADT");
                int chdCount = request.Passengers.Count(p => p.PaxType == "CHD");
                int infCount = request.Passengers.Count(p => p.PaxType == "INF");

                var bookingEntity = new Booking
                {
                    Id = Guid.NewGuid(),
                    UserId = resolvedUserId,
                    GuestSessionId = resolvedGuestSessionId,
                    BiletBankFileId = Guid.TryParse(preBookResult.ShoppingFileId, out var fId) ? fId : null,
                    PNR = preBookResult.BookingCode,
                    Status = preBookResult.Status ?? "PreBooked",
                    GrandTotal = preBookResult.TotalFare,
                    Currency = preBookResult.Currency ?? "TRY",
                    ServiceFee = preBookResult.ServiceFee,
                    IsFinalized = false,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    TransactionId = Guid.NewGuid().ToString(),
                    SessionId = allocateResult.SessionId,
                    SessionToken = allocateResult.SessionToken,
                    Origin = firstSeg?.OriginCode,
                    Destination = firstSeg?.DestinationCode,
                    AirlineCode = firstSeg?.MarketingAirline,
                    FlightNumber = firstSeg?.FlightNumber,
                    AllocatedAt = DateTime.UtcNow,
                    BookedAt = DateTime.UtcNow,
                    AdultCount = adtCount > 0 ? adtCount : 1,
                    ChildCount = chdCount,
                    InfantCount = infCount
                };

                foreach (var pax in passengersForUpdate)
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
                        Email = pax.SequenceNo == 1 ? request.ContactEmail : null,
                        Phone = pax.SequenceNo == 1 ? request.ContactPhone : null,
                        TempTag = pax.TempTag,
                        PaxReferenceId = pax.PaxReferenceId
                    });
                }

                // Use allocate segments (most complete — contains ALL legs for RT)
                // Fallback to preBookResult segments if allocate segments are missing
                var allSegments = airBooking?.Segments ?? [];
                var baggageStr = FormatBaggageAllowance(airBooking?.BaggageAllowances);

                if (allSegments.Count > 0)
                {
                    foreach (var seg in allSegments)
                    {
                        bookingEntity.FlightSegments.Add(new GBILET.Core.Entities.FlightSegment
                        {
                            Id = Guid.NewGuid(),
                            BookingId = bookingEntity.Id,
                            SequenceNo = bookingEntity.FlightSegments.Count + 1,
                            MarketingAirline = seg.MarketingAirline ?? "",
                            OperatingAirline = seg.OperatingAirline,
                            FlightNumber = seg.FlightNumber ?? "",
                            OriginCode = seg.OriginCode ?? "",
                            DestinationCode = seg.DestinationCode ?? "",
                            DepartureDate = DateTime.TryParse(seg.DepartureDay, out var depDt2) ? depDt2 : DateTime.MinValue,
                            DepartureTime = seg.DepartureTime,
                            ArrivalDate = DateTime.TryParse(seg.ArrivalDay, out var arrDt2) ? arrDt2 : null,
                            ArrivalTime = seg.ArrivalTime,
                            BookingClass = seg.BookingClass,
                            FareBasis = seg.FareBasis,
                            Baggage = baggageStr
                        });
                    }
                }
                else
                {
                    foreach (var seg in preBookResult.Segments)
                    {
                        bookingEntity.FlightSegments.Add(new GBILET.Core.Entities.FlightSegment
                        {
                            Id = Guid.NewGuid(),
                            BookingId = bookingEntity.Id,
                            SequenceNo = bookingEntity.FlightSegments.Count + 1,
                            MarketingAirline = seg.MarketingAirline ?? "",
                            FlightNumber = seg.FlightNumber ?? "",
                            OriginCode = seg.OriginCode ?? "",
                            DestinationCode = seg.DestinationCode ?? "",
                            DepartureDate = DateTime.TryParse(seg.DepartureDay, out var depDt) ? depDt : DateTime.MinValue,
                            DepartureTime = seg.DepartureTime,
                            ArrivalDate = DateTime.TryParse(seg.ArrivalDay, out var arrDt) ? arrDt : null,
                            ArrivalTime = seg.ArrivalTime,
                            BookingClass = seg.BookingClass
                        });
                    }
                }

                bookingEntity.FareDetails.Add(new FareDetail
                {
                    Id = Guid.NewGuid(),
                    BookingId = bookingEntity.Id,
                    BaseFare = preBookResult.BaseFare,
                    TotalTax = preBookResult.Taxes,
                    ServiceFee = preBookResult.ServiceFee,
                    GrandTotal = preBookResult.TotalFare,
                    Currency = preBookResult.Currency ?? "TRY",
                    CreatedAt = DateTime.UtcNow
                });

                bookingEntity.BookingLogs.Add(new BookingLog
                {
                    Id = Guid.NewGuid(),
                    BookingId = bookingEntity.Id,
                    SessionId = allocateResult.SessionId ?? "",
                    SessionToken = allocateResult.SessionToken ?? "",
                    Operation = "BookFlight_PreBooking",
                    IsSuccess = true,
                    CreatedAt = DateTime.UtcNow
                });

                await _bookingRepository.CreateBookingAsync(bookingEntity);
                savedBookingId = bookingEntity.Id;
                response.BookingId = savedBookingId;
                response.Steps.Add($"DB kaydı oluşturuldu. BookingId={savedBookingId}");
            }
            catch (Exception dbEx)
            {
                _logger.LogError(dbEx, "[BookFlight] DB kaydı başarısız ama akış devam ediyor.");
                response.Steps.Add($"DB kaydı başarısız: {dbEx.Message}");
            }

            // AutoPayAndFinalize = false ise burada dur
            if (!request.AutoPayAndFinalize)
            {
                response.Steps.Add("AutoPayAndFinalize=false — akış PreBooking'de durdu.");
                return Ok(response);
            }

            // ── ADIM 5: MakePayment ──
            response.Steps.Add($"Ödeme yapılıyor ({request.PaymentType})...");

            // ContinueUrl olustur ve session cache'le (BookFlight akisi icin)
            var bfCallbackUrl = BuildCallbackUrl();
            var bfContinueUrl = $"{bfCallbackUrl}?sid={Uri.EscapeDataString(allocateResult.SessionId!)}&stk={Uri.EscapeDataString(allocateResult.SessionToken!)}&sfid={Uri.EscapeDataString(preBookResult.ShoppingFileId!)}&bid={savedBookingId}";

            _cache.Set($"3d_session_{preBookResult.ShoppingFileId}", new ThreeDSessionData
            {
                SessionId = allocateResult.SessionId!,
                SessionToken = allocateResult.SessionToken!,
                ShoppingFileId = preBookResult.ShoppingFileId!,
                BookingId = savedBookingId,
                ProductId = airBooking?.ProductId
            }, TimeSpan.FromMinutes(15));

            var paymentRequest = new MakePaymentRequest
            {
                SessionId = allocateResult.SessionId!,
                SessionToken = allocateResult.SessionToken!,
                ShoppingFileId = preBookResult.ShoppingFileId!,
                ProductId = airBooking?.ProductId!,
                Amount = preBookResult.TotalFare,
                Currency = preBookResult.Currency ?? "TRY",
                PaymentType = request.PaymentType,
                CreditCard = request.CreditCard,
                BookingId = savedBookingId,
                ContinueUrl = bfContinueUrl
            };

            var paymentResult = await _flightService.MakePaymentAsync(paymentRequest);
            if (paymentResult.HasError)
            {
                response.HasError = true;
                response.ErrorMessage = $"Payment hatası: {paymentResult.ErrorMessage}";
                response.CompletedStep = "Payment";
                return Ok(response);
            }

            response.IsPaymentSuccessful = paymentResult.IsPaymentSuccessful;
            response.PaymentReferenceId = paymentResult.PaymentReferenceId;
            response.ThreeDSecureUrl = paymentResult.ThreeDSecureUrl;
            response.Is3DSecureRequired = paymentResult.Is3DSecureRequired;
            response.CompletedStep = "Payment";

            response.Steps.Add($"Ödeme tamamlandı. PaymentId={paymentResult.PaymentReferenceId}");

            // 3D Secure gerekiyorsa burada dur
            if (paymentResult.Is3DSecureRequired)
            {
                response.Steps.Add("3D Secure dogrulamasi gerekiyor — akis durdu. ThreeDSecureHtml'i kullaniciya gosterin.");
                response.Status = "Awaiting3DSecure";
                response.ThreeDSecureHtml = paymentResult.ThreeDSecureHtml;
                return Ok(response);
            }

            // DB güncelle
            if (savedBookingId.HasValue)
            {
                try
                {
                    await _bookingRepository.UpdateStatusAsync(savedBookingId.Value, "Paid");
                    await _bookingRepository.AddLogAsync(new BookingLog
                    {
                        Id = Guid.NewGuid(),
                        BookingId = savedBookingId.Value,
                        SessionId = allocateResult.SessionId ?? "",
                        SessionToken = allocateResult.SessionToken ?? "",
                        Operation = "BookFlight_Payment",
                        IsSuccess = true,
                        CreatedAt = DateTime.UtcNow
                    });
                }
                catch (Exception dbEx)
                {
                    _logger.LogError(dbEx, "[BookFlight] Payment DB güncelleme başarısız.");
                }
            }

            // ── ADIM 6: FinalizeShopping ──
            response.Steps.Add("Biletleme yapılıyor...");

            var finalizeRequest = new FinalizeShoppingRequest
            {
                SessionId = allocateResult.SessionId!,
                SessionToken = allocateResult.SessionToken!,
                ShoppingFileId = preBookResult.ShoppingFileId!,
                ProductId = airBooking?.ProductId!,
                BookingId = savedBookingId
            };

            var finalizeResult = await _flightService.FinalizeShoppingAsync(finalizeRequest);
            if (finalizeResult.HasError)
            {
                response.HasError = true;
                response.ErrorMessage = $"Finalize hatası: {finalizeResult.ErrorMessage}";
                response.CompletedStep = "Finalize";
                return Ok(response);
            }

            response.IsFinalized = true;
            response.PNR = finalizeResult.BookingCode ?? response.PNR;
            response.Status = finalizeResult.Status ?? "Ticketed";
            response.Tickets = finalizeResult.Tickets;
            response.CompletedStep = "Finalize";

            response.Steps.Add($"Biletleme tamamlandı! PNR={response.PNR}, Bilet sayısı={finalizeResult.Tickets.Count}");

            // DB güncelle
            if (savedBookingId.HasValue)
            {
                try
                {
                    var booking = await _bookingRepository.GetByIdAsync(savedBookingId.Value);
                    if (booking != null)
                    {
                        foreach (var ticket in finalizeResult.Tickets)
                        {
                            var pax = booking.Passengers.FirstOrDefault(p =>
                                string.Equals(p.FirstName, ticket.FirstName, StringComparison.OrdinalIgnoreCase) &&
                                string.Equals(p.LastName, ticket.LastName, StringComparison.OrdinalIgnoreCase));
                            if (pax != null)
                                pax.TicketNumber = ticket.TicketNumber;
                        }

                        // ReadShoppingFile to get ALL segments (outbound + return) with definitive data
                        try
                        {
                            var readResult = await _flightService.ReadShoppingFileAsync(new ReadShoppingFileRequest
                            {
                                SessionId = allocateResult.SessionId!,
                                SessionToken = allocateResult.SessionToken!,
                                ShoppingFileId = preBookResult.ShoppingFileId!
                            });

                            _logger.LogDebug("[BookFlight] ReadShoppingFile returned {Count} segments, HasError={HasError}", readResult?.Segments?.Count ?? 0, readResult?.HasError);
                            if (!readResult.HasError && readResult.Segments.Count > 0)
                            {
                                booking.FlightSegments.Clear();
                                var bagStr = FormatBaggageAllowance(airBooking?.BaggageAllowances);
                                foreach (var seg in readResult.Segments)
                                {
                                    booking.FlightSegments.Add(new GBILET.Core.Entities.FlightSegment
                                    {
                                        Id = Guid.NewGuid(),
                                        BookingId = booking.Id,
                                        SequenceNo = booking.FlightSegments.Count + 1,
                                        MarketingAirline = seg.MarketingAirline ?? "",
                                        FlightNumber = seg.FlightNumber ?? "",
                                        OriginCode = seg.OriginCode ?? "",
                                        DestinationCode = seg.DestinationCode ?? "",
                                        DepartureDate = DateTime.TryParse(seg.DepartureDay, out var rd) ? rd : DateTime.MinValue,
                                        DepartureTime = seg.DepartureTime,
                                        ArrivalDate = DateTime.TryParse(seg.ArrivalDay, out var ra) ? ra : null,
                                        ArrivalTime = seg.ArrivalTime,
                                        BookingClass = seg.BookingClass,
                                        Baggage = bagStr
                                    });
                                }
                                _logger.LogInformation("[BookFlight] Updated segments from ReadShoppingFile. Count={Count}", readResult.Segments.Count);
                            }
                        }
                        catch (Exception readEx)
                        {
                            _logger.LogWarning(readEx, "[BookFlight] ReadShoppingFile failed — keeping allocate segments.");
                        }

                        // InternalPnr uret ve kaydet
                        var internalPnr = await PnrGenerator.GenerateUniqueAsync(_bookingRepository);
                        booking.InternalPnr = internalPnr;
                        await _bookingRepository.UpdateInternalPnrAsync(savedBookingId.Value, internalPnr);
                        response.InternalPnr = internalPnr;

                        await _bookingRepository.UpdateStatusAsync(savedBookingId.Value, response.Status!);
                        await _bookingRepository.AddLogAsync(new BookingLog
                        {
                            Id = Guid.NewGuid(),
                            BookingId = savedBookingId.Value,
                            SessionId = allocateResult.SessionId ?? "",
                            SessionToken = allocateResult.SessionToken ?? "",
                            Operation = "BookFlight_Finalize",
                            IsSuccess = true,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }
                catch (Exception dbEx)
                {
                    _logger.LogError(dbEx, "[BookFlight] Finalize DB güncelleme başarısız.");
                }
            }

            response.Steps.Add("✅ Tüm adımlar başarıyla tamamlandı!");
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BookFlight] Beklenmeyen hata");
            response.HasError = true;
            response.ErrorMessage = "Rezervasyon işlemi sırasında beklenmeyen bir hata oluştu.";
            response.Steps.Add("HATA: Beklenmeyen bir hata oluştu.");
            return StatusCode(500, response);
        }
    }

    private static string? FormatBaggageAllowance(List<AllocateBaggageAllowance>? allowances)
    {
        if (allowances == null || allowances.Count == 0) return null;
        var first = allowances[0];
        if (!string.IsNullOrWhiteSpace(first.Allowance) && !string.IsNullOrWhiteSpace(first.Unit))
            return $"{first.Allowance} {first.Unit}";
        if (!string.IsNullOrWhiteSpace(first.Allowance))
            return first.Allowance;
        return null;
    }

    /// <summary>
    /// Telefon numarasini BiletBank'in beklediği +CC-XXXXXXXXXX formatina donusturur.
    /// Ornek: +90-5351234567, +971-501234567, +1-2025551234
    /// </summary>
    private static string NormalizePhoneNumber(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return "+90-5000000000";

        // Zaten +CC-XXX formatindaysa dokunma
        if (phone.StartsWith("+") && phone.Contains('-'))
            return phone;

        var digits = new string(phone.Where(char.IsDigit).ToArray());

        // +905351234567 veya 905351234567 (12 hane, 90 ile basliyor)
        if (digits.Length == 12 && digits.StartsWith("90"))
            return $"+90-{digits[2..]}";

        // 05351234567 (11 hane, 0 ile basliyor)
        if (digits.Length == 11 && digits.StartsWith("0"))
            return $"+90-{digits[1..]}";

        // 5351234567 (10 hane — TR varsay)
        if (digits.Length == 10)
            return $"+90-{digits}";

        return phone.StartsWith("+") ? phone : $"+{phone}";
    }
}