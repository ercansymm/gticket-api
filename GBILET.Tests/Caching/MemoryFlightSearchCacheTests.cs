using FluentAssertions;
using GBILET.Core.Models.Flight;
using GBILET.Infrastructure.Caching;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace GBILET.Tests.Caching;

public class MemoryFlightSearchCacheTests
{
    private static MemoryFlightSearchCache BuildCache(int slidingMin = 5, int absoluteMin = 15, long sizeLimit = 100)
    {
        var options = Options.Create(new FlightCacheOptions
        {
            SlidingMinutes = slidingMin,
            AbsoluteMinutes = absoluteMin,
            SizeLimit = sizeLimit
        });
        return new MemoryFlightSearchCache(NullLogger<MemoryFlightSearchCache>.Instance, options);
    }

    private sealed class FakeResponse
    {
        public bool HasError { get; set; }
        public string? Payload { get; set; }
    }

    [Fact]
    public async Task GetOrFetchAsync_calls_factory_once_on_cache_miss()
    {
        using var cache = BuildCache();
        var counter = 0;

        var first = await cache.GetOrFetchAsync<FakeResponse>("k1", _ =>
        {
            counter++;
            return Task.FromResult<FakeResponse?>(new FakeResponse { Payload = "ok" });
        });
        var second = await cache.GetOrFetchAsync<FakeResponse>("k1", _ =>
        {
            counter++;
            return Task.FromResult<FakeResponse?>(new FakeResponse { Payload = "should-not-run" });
        });

        counter.Should().Be(1);
        first!.Payload.Should().Be("ok");
        second!.Payload.Should().Be("ok");
    }

    [Fact]
    public async Task GetOrFetchAsync_under_concurrent_load_runs_factory_once()
    {
        using var cache = BuildCache();
        var counter = 0;
        var gate = new TaskCompletionSource();

        async Task<FakeResponse?> Factory(CancellationToken _)
        {
            Interlocked.Increment(ref counter);
            await gate.Task;
            return new FakeResponse { Payload = "fresh" };
        }

        var tasks = Enumerable.Range(0, 100)
            .Select(_ => cache.GetOrFetchAsync<FakeResponse>("burst", Factory))
            .ToList();

        gate.SetResult();
        var results = await Task.WhenAll(tasks);

        counter.Should().Be(1);
        results.Should().AllSatisfy(r => r!.Payload.Should().Be("fresh"));
    }

    [Fact]
    public async Task GetOrFetchAsync_does_not_cache_HasError_response()
    {
        using var cache = BuildCache();
        var counter = 0;

        await cache.GetOrFetchAsync<FakeResponse>("err",
            _ => { counter++; return Task.FromResult<FakeResponse?>(new FakeResponse { HasError = true }); });

        await cache.GetOrFetchAsync<FakeResponse>("err",
            _ => { counter++; return Task.FromResult<FakeResponse?>(new FakeResponse { HasError = true }); });

        counter.Should().Be(2, "HasError responses must never be cached");
    }

    [Fact]
    public async Task TryGetSnapshot_returns_false_on_miss()
    {
        using var cache = BuildCache();
        var hit = cache.TryGetSnapshot<FakeResponse>("nope", out var value);
        hit.Should().BeFalse();
        value.Should().BeNull();

        await cache.GetOrFetchAsync<FakeResponse>("nope",
            _ => Task.FromResult<FakeResponse?>(new FakeResponse { Payload = "x" }));

        cache.TryGetSnapshot<FakeResponse>("nope", out var v2).Should().BeTrue();
        v2!.Payload.Should().Be("x");
    }

    [Fact]
    public async Task Remove_invalidates_entry()
    {
        using var cache = BuildCache();
        var counter = 0;

        await cache.GetOrFetchAsync<FakeResponse>("k",
            _ => { counter++; return Task.FromResult<FakeResponse?>(new FakeResponse { Payload = "1" }); });

        cache.Remove("k");

        await cache.GetOrFetchAsync<FakeResponse>("k",
            _ => { counter++; return Task.FromResult<FakeResponse?>(new FakeResponse { Payload = "2" }); });

        counter.Should().Be(2);
    }
}
