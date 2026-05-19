using System.Collections.Concurrent;
using System.Reflection;
using GBILET.Core.Interfaces;
using GBILET.Core.Models.Flight;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GBILET.Infrastructure.Caching;

/// <summary>
/// IMemoryCache uzerine kurulu, per-key SemaphoreSlim ile stampede koruma yapan
/// AirSearch cache implementasyonu. Sliding + absolute expiration kullanir,
/// HasError=true response'lari cache'lemez. DB log uretmez, yalnizca debug log atar.
/// Diger katmanlardaki IMemoryCache kullanimini etkilememek icin kendi izole
/// MemoryCache instance'ini olusturur (SizeLimit yalnizca bu instance'ta gecerli).
/// </summary>
public class MemoryFlightSearchCache : IFlightSearchCache, IDisposable
{
    private readonly MemoryCache _cache;
    private readonly ILogger<MemoryFlightSearchCache> _logger;
    private readonly FlightCacheOptions _options;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public MemoryFlightSearchCache(
        ILogger<MemoryFlightSearchCache> logger,
        IOptions<FlightCacheOptions> options)
    {
        _logger = logger;
        _options = options.Value;
        _cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = _options.SizeLimit });
    }

    public async Task<T?> GetOrFetchAsync<T>(
        string key,
        Func<CancellationToken, Task<T?>> factory,
        CancellationToken ct = default) where T : class
    {
        if (_cache.TryGetValue(key, out T? cached) && cached is not null)
        {
            _logger.LogDebug("Cache {Status} for key {Key}", "HIT", key);
            return cached;
        }

        var gate = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Double-check after acquiring lock
            if (_cache.TryGetValue(key, out cached) && cached is not null)
            {
                _logger.LogDebug("Cache {Status} for key {Key}", "HIT", key);
                return cached;
            }

            _logger.LogDebug("Cache {Status} for key {Key}", "MISS", key);
            var fresh = await factory(ct).ConfigureAwait(false);

            if (fresh is null || HasErrorFlag(fresh))
            {
                _logger.LogDebug("Cache skip-write for key {Key} (null or HasError)", key);
                return fresh;
            }

            var entryOptions = new MemoryCacheEntryOptions
            {
                Size = 1,
                SlidingExpiration = TimeSpan.FromMinutes(_options.SlidingMinutes),
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.AbsoluteMinutes)
            };

            _cache.Set(key, fresh, entryOptions);
            return fresh;
        }
        finally
        {
            gate.Release();
        }
    }

    public bool TryGetSnapshot<T>(string key, out T? value) where T : class
    {
        if (_cache.TryGetValue(key, out T? cached) && cached is not null)
        {
            _logger.LogDebug("Cache snapshot {Status} for key {Key}", "HIT", key);
            value = cached;
            return true;
        }

        _logger.LogDebug("Cache snapshot {Status} for key {Key}", "MISS", key);
        value = null;
        return false;
    }

    public void Remove(string key)
    {
        _cache.Remove(key);
        _logger.LogDebug("Cache invalidated for key {Key}", key);
    }

    /// <summary>
    /// Reflection ile generic response objesi uzerindeki HasError property'sini okur.
    /// FlightSearchResponseDto/AirSearchResponse gibi sinifin HasError=true olmasi cache yazimini engeller.
    /// </summary>
    private static bool HasErrorFlag(object value)
    {
        var prop = value.GetType().GetProperty("HasError", BindingFlags.Public | BindingFlags.Instance);
        if (prop == null || prop.PropertyType != typeof(bool)) return false;
        return prop.GetValue(value) is true;
    }

    public void Dispose()
    {
        _cache.Dispose();
        foreach (var sem in _locks.Values) sem.Dispose();
        _locks.Clear();
        GC.SuppressFinalize(this);
    }
}
