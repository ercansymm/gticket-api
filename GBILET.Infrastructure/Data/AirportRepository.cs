using GBILET.Core.Entities;
using GBILET.Core.Service;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace GBILET.Infrastructure.Data;

public class AirportRepository : IAirportRepository
{
    private readonly GTicketDbContext _db;
    private readonly IMemoryCache _cache;

    private const string CacheKeyAll = "airports_all";
    private const string CacheKeyDomestic = "airports_domestic";
    private const string CacheKeyInternational = "airports_international";
    private const string CacheKeyPopular = "airports_popular";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(6);

    public AirportRepository(GTicketDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<List<Airport>> GetAllAsync(int page = 1, int pageSize = 50)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 500) pageSize = 500;

        return await _db.Airports
            .Where(a => a.IsActive)
            .OrderBy(a => a.SortOrder)
            .ThenBy(a => a.CityEn)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<int> GetTotalCountAsync()
    {
        return await _db.Airports.CountAsync(a => a.IsActive);
    }

    public async Task<List<Airport>> GetDomesticAsync()
    {
        return await _cache.GetOrCreateAsync(CacheKeyDomestic, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return await _db.Airports
                .Where(a => a.IsActive && a.IsDomestic)
                .OrderBy(a => a.SortOrder)
                .AsNoTracking()
                .ToListAsync();
        }) ?? [];
    }

    public async Task<List<Airport>> GetInternationalAsync()
    {
        return await _cache.GetOrCreateAsync(CacheKeyInternational, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return await _db.Airports
                .Where(a => a.IsActive && !a.IsDomestic)
                .OrderBy(a => a.SortOrder)
                .ThenBy(a => a.CityEn)
                .AsNoTracking()
                .ToListAsync();
        }) ?? [];
    }

    public async Task<List<Airport>> GetPopularAsync()
    {
        return await _cache.GetOrCreateAsync(CacheKeyPopular, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return await _db.Airports
                .Where(a => a.IsActive && a.IsPopular)
                .OrderBy(a => a.SortOrder)
                .AsNoTracking()
                .ToListAsync();
        }) ?? [];
    }

    public async Task<List<Airport>> GetByCountryAsync(string countryCode)
    {
        var code = countryCode.ToUpperInvariant();
        var cacheKey = $"airports_country_{code}";

        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return await _db.Airports
                .Where(a => a.IsActive && a.CountryCode == code)
                .OrderBy(a => a.SortOrder)
                .ThenBy(a => a.CityEn)
                .AsNoTracking()
                .ToListAsync();
        }) ?? [];
    }

    public async Task<Airport?> GetByIataCodeAsync(string iataCode)
    {
        var code = iataCode.ToUpperInvariant();
        return await _db.Airports
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.IataCode == code);
    }

    public async Task<List<Airport>> SearchAsync(string query, int limit = 10)
    {
        if (limit < 1) limit = 1;
        if (limit > 50) limit = 50;

        var q = NormalizeTurkish(query.Trim().ToLowerInvariant());

        var allAirports = await _cache.GetOrCreateAsync(CacheKeyAll, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return await _db.Airports
                .Where(a => a.IsActive)
                .OrderBy(a => a.SortOrder)
                .AsNoTracking()
                .ToListAsync();
        }) ?? [];

        var results = allAirports
            .Select(a => new
            {
                Airport = a,
                Score = CalculateSearchScore(a, q)
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Airport.SortOrder)
            .Take(limit)
            .Select(x => x.Airport)
            .ToList();

        return results;
    }

    private static int CalculateSearchScore(Airport airport, string normalizedQuery)
    {
        var iata = airport.IataCode.ToLowerInvariant();
        var nameTr = NormalizeTurkish(airport.NameTr.ToLowerInvariant());
        var nameEn = airport.NameEn.ToLowerInvariant();
        var cityTr = NormalizeTurkish(airport.CityTr.ToLowerInvariant());
        var cityEn = airport.CityEn.ToLowerInvariant();

        // Exact IATA code match � highest priority
        if (iata == normalizedQuery) return 1000;

        // IATA code starts with query
        if (iata.StartsWith(normalizedQuery)) return 900;

        int score = 0;

        // City name exact match
        if (cityTr == normalizedQuery || cityEn == normalizedQuery) score = Math.Max(score, 800);

        // City name starts with query
        if (cityTr.StartsWith(normalizedQuery) || cityEn.StartsWith(normalizedQuery)) score = Math.Max(score, 700);

        // Airport name starts with query
        if (nameTr.StartsWith(normalizedQuery) || nameEn.StartsWith(normalizedQuery)) score = Math.Max(score, 600);

        // Contains match
        if (cityTr.Contains(normalizedQuery) || cityEn.Contains(normalizedQuery)) score = Math.Max(score, 500);
        if (nameTr.Contains(normalizedQuery) || nameEn.Contains(normalizedQuery)) score = Math.Max(score, 400);

        // Fuzzy: Levenshtein distance for short queries (typo tolerance)
        if (normalizedQuery.Length >= 3)
        {
            if (FuzzyContains(cityTr, normalizedQuery) || FuzzyContains(cityEn, normalizedQuery))
                score = Math.Max(score, 200);
            if (FuzzyContains(nameTr, normalizedQuery) || FuzzyContains(nameEn, normalizedQuery))
                score = Math.Max(score, 100);
        }

        // Boost popular airports
        if (score > 0 && airport.IsPopular) score += 50;

        return score;
    }

    private static bool FuzzyContains(string text, string query)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(query)) return false;
        if (text.Contains(query)) return true;

        // Simple sliding window fuzzy match: allow 1 character difference
        for (int i = 0; i <= text.Length - query.Length; i++)
        {
            var window = text.Substring(i, query.Length);
            if (LevenshteinDistance(window, query) <= 1)
                return true;
        }
        return false;
    }

    private static int LevenshteinDistance(string s, string t)
    {
        if (string.IsNullOrEmpty(s)) return t?.Length ?? 0;
        if (string.IsNullOrEmpty(t)) return s.Length;

        var d = new int[s.Length + 1, t.Length + 1];
        for (int i = 0; i <= s.Length; i++) d[i, 0] = i;
        for (int j = 0; j <= t.Length; j++) d[0, j] = j;

        for (int i = 1; i <= s.Length; i++)
        {
            for (int j = 1; j <= t.Length; j++)
            {
                int cost = s[i - 1] == t[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }
        return d[s.Length, t.Length];
    }

    private static string NormalizeTurkish(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        return input
            .Replace('�', 'i')
            .Replace('�', 'i')
            .Replace('�', 'g')
            .Replace('�', 'g')
            .Replace('�', 'u')
            .Replace('�', 'u')
            .Replace('�', 's')
            .Replace('�', 's')
            .Replace('�', 'o')
            .Replace('�', 'o')
            .Replace('�', 'c')
            .Replace('�', 'c');
    }
}
