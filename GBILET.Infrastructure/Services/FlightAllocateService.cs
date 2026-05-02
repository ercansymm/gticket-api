using GBILET.Core.Interfaces;
using GBILET.Core.Models.Flight;
using GBILET.Core.Service.Flight;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GBILET.Infrastructure.Services;

/// <summary>
/// IFlightService.AllocateFlightAsync uzerine kurulu, cache snapshot karsilastirmasi yapan
/// orchestrator. Provider'a HER ZAMAN gider (allocate cache'lenmez); cache sadece
/// onceki search yanitiyla degisiklik tespiti icin kullanilir.
/// </summary>
public class FlightAllocateService
{
    private readonly IFlightService _flightService;
    private readonly IFlightSearchCache _cache;
    private readonly FlightChangeDetector _detector;
    private readonly ILogger<FlightAllocateService> _logger;
    private readonly FlightCacheOptions _cacheOptions;

    public FlightAllocateService(
        IFlightService flightService,
        IFlightSearchCache cache,
        FlightChangeDetector detector,
        IOptions<FlightCacheOptions> cacheOptions,
        ILogger<FlightAllocateService> logger)
    {
        _flightService = flightService;
        _cache = cache;
        _detector = detector;
        _cacheOptions = cacheOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// Cache snapshot okur, fresh allocate cagrisi yapar, FlightChangeDetector ile karsilastirir.
    /// Sonuc CanProceedToCheckout=false ise frontend ya search'e geri yonlendirmeli ya da
    /// onay modali gostermelidir.
    /// </summary>
    public async Task<AllocateResult> AllocateAsync(
        AllocateRequest request,
        SearchRequest? originalSearchCriteria = null,
        string currency = "TRY",
        CancellationToken ct = default)
    {
        // 1) Provider'a HER ZAMAN git (allocate cache'lenmez)
        var allocate = await _flightService.AllocateFlightAsync(request).ConfigureAwait(false);

        var result = new AllocateResult { Allocate = allocate };

        // 2) Provider hatasi ise change detection yapilmaz
        if (allocate.HasError)
        {
            result.Change = new FlightChangeResult { HasChanges = false, CanContinue = false, Type = FlightChangeType.NoChange };
            result.CanProceedToCheckout = false;
            return result;
        }

        // 3) Cache snapshot karsilastirmasi (criteria yoksa atla)
        if (originalSearchCriteria == null)
        {
            result.Change = new FlightChangeResult { HasChanges = false, CanContinue = true, Type = FlightChangeType.NoChange };
            result.CanProceedToCheckout = true;
            return result;
        }

        var cacheKey = FlightSearchKeyGenerator.Build(originalSearchCriteria, _cacheOptions.KeyVersion, currency);
        FlightResultDto? cachedFlight = null;
        FlightResultDto? freshFlight = null;

        if (_cache.TryGetSnapshot<FlightSearchResponseDto>(cacheKey, out var cachedSearch) && cachedSearch != null)
        {
            cachedFlight = cachedSearch.Flights.FirstOrDefault(f => f.ProductId == request.ProductId);
        }

        // Allocate response uzerinden fresh DTO simulate et (fiyat/koltuk karsilastirma icin)
        freshFlight = MapAllocateToFlightDto(allocate, request.ProductId, cachedFlight);

        result.Change = await _detector.DetectAsync(cachedFlight, freshFlight, originalSearchCriteria, ct).ConfigureAwait(false);
        result.CanProceedToCheckout = result.Change.CanContinue;

        return result;
    }

    /// <summary>
    /// Allocate response'undaki fiyat ve uygunluk bilgilerini, cached FlightResultDto sablonuna
    /// uygulayarak fresh karsilastirma snapshot'i uretir. Boylece detector ayni sema uzerinde calisir.
    /// </summary>
    private static FlightResultDto MapAllocateToFlightDto(AllocateResponse allocate, string productId, FlightResultDto? template)
    {
        var booking = allocate.AirBookings.FirstOrDefault(b => b.ProductId == productId)
                      ?? allocate.AirBookings.FirstOrDefault();

        var dto = new FlightResultDto
        {
            ProductId = productId,
            TotalFare = booking?.TotalFare ?? template?.TotalFare ?? 0,
            BaseFare = booking?.BaseFare ?? template?.BaseFare ?? 0,
            Taxes = booking?.Taxes ?? template?.Taxes ?? 0,
            Currency = booking?.Currency ?? allocate.Currency ?? template?.Currency,
            IsReservable = allocate.CanBeReserved,
            // BiletBank allocate response'u koltuk sayisini her zaman dondurmez;
            // CanBeReserved=false ise 0 kabul ediyoruz, true ise template'i koruyoruz
            AvailableSeats = allocate.CanBeReserved ? (template?.AvailableSeats ?? 9) : 0,
            DepartureDate = template?.DepartureDate,
            DepartureTime = template?.DepartureTime,
            AirlineCode = template?.AirlineCode,
            FlightNumber = template?.FlightNumber
        };

        // Allocate segment bilgisi varsa (provider'dan gelen taze veriyi tercih et)
        var firstSegment = booking?.Segments?.FirstOrDefault();
        if (firstSegment != null)
        {
            dto.DepartureDate = firstSegment.DepartureDay ?? dto.DepartureDate;
            dto.DepartureTime = firstSegment.DepartureTime ?? dto.DepartureTime;
        }

        return dto;
    }
}
