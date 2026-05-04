using GBILET.Core.Interfaces;
using GBILET.Core.Models.Flight;
using GBILET.Core.Service.Flight;
using GBILET.Infrastructure.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GBILET.Infrastructure.Services;

/// <summary>
/// IFlightService.AllocateFlightAsync uzerine kurulu, cache snapshot karsilastirmasi yapan
/// orchestrator. Provider'a HER ZAMAN gider (allocate cache'lenmez); cache sadece
/// onceki search yanitiyla degisiklik tespiti icin kullanilir.
/// SessionRecoveryExecutor ile sarmaldir; BiletBank session expire durumunda
/// otomatik olarak yeni search yapar ve allocate'i tek seferlik yeniden dener.
/// </summary>
public class FlightAllocateService
{
    private readonly IFlightService _flightService;
    private readonly IFlightSearchCache _cache;
    private readonly FlightChangeDetector _detector;
    private readonly SessionRecoveryExecutor _recoveryExecutor;
    private readonly ILogger<FlightAllocateService> _logger;
    private readonly FlightCacheOptions _cacheOptions;

    public FlightAllocateService(
        IFlightService flightService,
        IFlightSearchCache cache,
        FlightChangeDetector detector,
        SessionRecoveryExecutor recoveryExecutor,
        IOptions<FlightCacheOptions> cacheOptions,
        ILogger<FlightAllocateService> logger)
    {
        _flightService = flightService;
        _cache = cache;
        _detector = detector;
        _recoveryExecutor = recoveryExecutor;
        _cacheOptions = cacheOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// Cache snapshot okur, fresh allocate cagrisi yapar, FlightChangeDetector ile karsilastirir.
    /// Session expire durumunda SessionRecoveryExecutor otomatik olarak yeni search ile recovery yapar.
    /// </summary>
    public async Task<AllocateResult> AllocateAsync(
        AllocateRequest request,
        SearchRequest? originalSearchCriteria = null,
        string currency = "TRY",
        CancellationToken ct = default)
    {
        return await _recoveryExecutor.ExecuteAsync(
            action: () => AllocateOnceAsync(request, originalSearchCriteria, currency, ct),
            recoveryAction: () => RecoverSessionAsync(request, originalSearchCriteria, currency, ct),
            maxRetries: 1,
            ct: ct).ConfigureAwait(false);
    }

    private async Task<AllocateResult> AllocateOnceAsync(
        AllocateRequest request,
        SearchRequest? originalSearchCriteria,
        string currency,
        CancellationToken ct)
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

        if (_cache.TryGetSnapshot<FlightSearchResponseDto>(cacheKey, out var cachedSearch) && cachedSearch != null)
        {
            cachedFlight = cachedSearch.Flights.FirstOrDefault(f => f.ProductId == request.ProductId);
        }

        var freshFlight = MapAllocateToFlightDto(allocate, request.ProductId, cachedFlight);

        result.Change = await _detector.DetectAsync(cachedFlight, freshFlight, originalSearchCriteria, ct).ConfigureAwait(false);
        result.CanProceedToCheckout = result.Change.CanContinue;

        // Backward-compat: change bilgisini AllocateResponse uzerine de yaz
        allocate.HasChanges = result.Change.HasChanges;
        allocate.CanProceedToCheckout = result.CanProceedToCheckout;
        allocate.ChangeType = result.Change.Type.ToString();
        allocate.OldPrice = result.Change.OldPrice;
        allocate.NewPrice = result.Change.NewPrice;
        allocate.OldDepartureTime = result.Change.OldDepartureTime;
        allocate.NewDepartureTime = result.Change.NewDepartureTime;
        allocate.UserMessage = result.Change.UserMessage;

        return result;
    }

    /// <summary>
    /// Session expire durumunda cagrilir: cache invalidate, session bilgilerini sifirla, yeni search yap.
    /// AllocateOnceAsync sonraki cagrida session bos oldugu icin BiletBankFlightService kendi icinde
    /// login + search + allocate akisini calistirir.
    /// </summary>
    private async Task RecoverSessionAsync(
        AllocateRequest request,
        SearchRequest? originalSearchCriteria,
        string currency,
        CancellationToken ct)
    {
        _logger.LogWarning("[FlightAllocate] Session expire detected, invalidating cache and resetting session for retry.");

        if (originalSearchCriteria != null)
        {
            var cacheKey = FlightSearchKeyGenerator.Build(originalSearchCriteria, _cacheOptions.KeyVersion, currency);
            _cache.Remove(cacheKey);

            // Cache'i taze veriyle yeniden doldur (frontend bir sonraki turda eski snapshot'i gormez)
            try
            {
                var fresh = await _flightService.SearchFlightDtoAsync(originalSearchCriteria).ConfigureAwait(false);
                _logger.LogInformation("[FlightAllocate] Recovery search completed. NewSessionId={SessionId}", fresh?.SessionId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[FlightAllocate] Recovery search failed; allocate retry will perform its own login.");
            }
        }

        // Session bilgilerini temizle: AllocateFlightAsync kendi icinde login+search+allocate yapacak
        request.SessionId = null;
        request.SessionToken = null;
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
            AvailableSeats = allocate.CanBeReserved ? (template?.AvailableSeats ?? 9) : 0,
            DepartureDate = template?.DepartureDate,
            DepartureTime = template?.DepartureTime,
            AirlineCode = template?.AirlineCode,
            FlightNumber = template?.FlightNumber
        };

        var firstSegment = booking?.Segments?.FirstOrDefault();
        if (firstSegment != null)
        {
            dto.DepartureDate = firstSegment.DepartureDay ?? dto.DepartureDate;
            dto.DepartureTime = firstSegment.DepartureTime ?? dto.DepartureTime;
        }

        return dto;
    }
}
