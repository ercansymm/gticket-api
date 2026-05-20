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
    /// Recoverable allocate hatasinda cagrilir: cache invalidate, yeni search yap, eski productId'yi
    /// yeni search'teki ayni ucusun yeni productId'si ile degistir, session bilgilerini sifirla.
    /// BiletBank her search'te yeni UUID urettigi icin eski productId stale kalir — identity match
    /// (airline + flight number + departure date/time + origin/destination) ile yeni productId bulunur.
    /// Bu olmadan retry "Product not found" ile yine patlar.
    /// </summary>
    private async Task RecoverSessionAsync(
        AllocateRequest request,
        SearchRequest? originalSearchCriteria,
        string currency,
        CancellationToken ct)
    {
        _logger.LogWarning(
            "[FlightAllocate] Recoverable fault detected. Invalidating cache, fresh search and remapping productId. OldProductId={OldProductId}",
            request.ProductId);

        if (originalSearchCriteria == null)
        {
            request.SessionId = null;
            request.SessionToken = null;
            return;
        }

        var cacheKey = FlightSearchKeyGenerator.Build(originalSearchCriteria, _cacheOptions.KeyVersion, currency);

        // 1) Eski snapshot'tan secilen ucusu identity icin oku (yeni search'teyse fiyat veya UUID degisebilir)
        FlightResultDto? oldOutbound = null;
        FlightResultDto? oldReturn = null;
        if (_cache.TryGetSnapshot<FlightSearchResponseDto>(cacheKey, out var oldSnapshot) && oldSnapshot != null)
        {
            oldOutbound = oldSnapshot.Flights.FirstOrDefault(f => f.ProductId == request.ProductId);
            if (!string.IsNullOrEmpty(request.ReturnProductId))
                oldReturn = oldSnapshot.Flights.FirstOrDefault(f => f.ProductId == request.ReturnProductId);
        }

        _cache.Remove(cacheKey);

        // 2) Fresh search — yeni productId'ler ve sessionId burada uretilir
        FlightSearchResponseDto? fresh = null;
        try
        {
            fresh = await _flightService.SearchFlightDtoAsync(originalSearchCriteria).ConfigureAwait(false);
            _logger.LogInformation("[FlightAllocate] Recovery search completed. NewSessionId={SessionId}, FlightCount={Count}",
                fresh?.SessionId, fresh?.Flights.Count ?? 0);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[FlightAllocate] Recovery search failed; allocate retry will perform its own login.");
        }

        // 3) Identity match: yeni search'te ayni ucusu bul ve productId/brandedFareItemId'leri swap et
        if (fresh != null && oldOutbound != null)
        {
            var newOutbound = FindMatchingFlight(fresh.Flights, oldOutbound);
            if (newOutbound != null && !string.IsNullOrEmpty(newOutbound.ProductId))
            {
                _logger.LogInformation(
                    "[FlightAllocate] Outbound productId remapped: {Old} -> {New} (flight {Airline}{FlightNo} {Date} {Time})",
                    request.ProductId, newOutbound.ProductId,
                    newOutbound.AirlineCode, newOutbound.FlightNumber, newOutbound.DepartureDate, newOutbound.DepartureTime);
                request.ProductId = newOutbound.ProductId;
                // BrandedFareItemId yeni search'te farkli UUID'ler aliyor; default'a (null) birak,
                // BB Allocate'i kendi default brand'ini secer. Kullanici checkout'ta yeniden secebilir.
                request.BrandedFareItemId = null;
            }
            else
            {
                _logger.LogWarning(
                    "[FlightAllocate] Outbound no longer available after recovery search. AirlineCode={Airline} FlightNumber={FlightNo}",
                    oldOutbound.AirlineCode, oldOutbound.FlightNumber);
            }
        }

        if (fresh != null && oldReturn != null)
        {
            var newReturn = FindMatchingFlight(fresh.Flights, oldReturn);
            if (newReturn != null && !string.IsNullOrEmpty(newReturn.ProductId))
            {
                _logger.LogInformation("[FlightAllocate] Return productId remapped: {Old} -> {New}",
                    request.ReturnProductId, newReturn.ProductId);
                request.ReturnProductId = newReturn.ProductId;
                request.ReturnBrandedFareItemId = null;
            }
        }

        // 4) Recovery search'in session'ini request'e ata — BU KRITIK!
        //    Aksi halde AllocateFlightAsync "session yoksa login+search+allocate" dalina girip
        //    UCUNCU bir search yapar ve productId'lere yine yeni UUID atanir. Recovery'de
        //    bulunan productId stale olur, retry "Already allocated" ile yine patlar.
        //    Recovery'nin yarattigi session ile direkt allocate calistirilmali.
        if (fresh != null && !string.IsNullOrEmpty(fresh.SessionId) && !string.IsNullOrEmpty(fresh.SessionToken))
        {
            request.SessionId = fresh.SessionId;
            request.SessionToken = fresh.SessionToken;
            _logger.LogInformation(
                "[FlightAllocate] Retry will use recovery session: SessionId={SessionId}",
                fresh.SessionId);
        }
        else
        {
            // Recovery search basarisizsa fallback: AllocateFlightAsync kendi login+search'unu yapsin.
            // Bu durumda productId yine stale olabilir ama tek seferlik denenip biter.
            request.SessionId = null;
            request.SessionToken = null;
            _logger.LogWarning("[FlightAllocate] Recovery search yielded no session; retry will start its own login.");
        }
    }

    /// <summary>
    /// Iki FlightResultDto'nun ayni "ucus" oldugunu identity ile dogrular: havayolu, ucus no,
    /// kalkis tarihi ve saati, origin/destination. Fiyat ve UUID disinda kalan alanlar her search'te ayni.
    /// </summary>
    private static FlightResultDto? FindMatchingFlight(IEnumerable<FlightResultDto> flights, FlightResultDto target)
    {
        return flights.FirstOrDefault(f =>
            !string.IsNullOrEmpty(f.ProductId) &&
            string.Equals(f.AirlineCode, target.AirlineCode, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(f.FlightNumber, target.FlightNumber, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(f.DepartureDate, target.DepartureDate, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(f.DepartureTime, target.DepartureTime, StringComparison.OrdinalIgnoreCase));
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
