namespace GBILET.Core.Interfaces;

/// <summary>
/// AirSearch sonuclarini in-memory cache'leyen katmanin sozlesmesi.
/// Cache hit/miss yonetimi tamamen seffaftir; cagiran kod miss/hit farkini gormez.
/// DB log mekanizmasina dokunmaz, sadece teknik debug log uretir.
/// </summary>
public interface IFlightSearchCache
{
    /// <summary>
    /// Verilen anahtar icin cache hit ise dondurur, miss ise factory'yi calistirip sonucu cache'ler.
    /// Per-key SemaphoreSlim ile cache stampede koruma uygulanir.
    /// </summary>
    Task<T?> GetOrFetchAsync<T>(string key, Func<CancellationToken, Task<T?>> factory, CancellationToken ct = default)
        where T : class;

    /// <summary>
    /// Cache'i sessizce okur (allocate karsilastirma icin). Cache miss olursa false doner, factory tetiklemez.
    /// </summary>
    bool TryGetSnapshot<T>(string key, out T? value) where T : class;

    /// <summary>Belirli bir cache anahtarini invalidate eder (admin commission update gibi tetikleyiciler icin).</summary>
    void Remove(string key);
}
