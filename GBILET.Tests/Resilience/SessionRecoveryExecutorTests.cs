using FluentAssertions;
using GBILET.Core.Exceptions;
using GBILET.Infrastructure.Resilience;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GBILET.Tests.Resilience;

public class SessionRecoveryExecutorTests
{
    private static SessionRecoveryExecutor Build()
        => new(NullLogger<SessionRecoveryExecutor>.Instance);

    [Fact]
    public async Task ExecuteAsync_returns_action_result_when_no_exception()
    {
        var executor = Build();
        var recoveryCalls = 0;

        var result = await executor.ExecuteAsync(
            action: () => Task.FromResult(42),
            recoveryAction: () => { recoveryCalls++; return Task.CompletedTask; });

        result.Should().Be(42);
        recoveryCalls.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_runs_recovery_once_and_retries_action_on_session_expired()
    {
        var executor = Build();
        var actionCalls = 0;
        var recoveryCalls = 0;

        var result = await executor.ExecuteAsync(
            action: () =>
            {
                actionCalls++;
                if (actionCalls == 1)
                    throw new BiletBankSessionExpiredException("expired");
                return Task.FromResult("ok");
            },
            recoveryAction: () => { recoveryCalls++; return Task.CompletedTask; });

        result.Should().Be("ok");
        actionCalls.Should().Be(2);
        recoveryCalls.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_throws_when_action_fails_again_after_recovery()
    {
        var executor = Build();
        var actionCalls = 0;
        var recoveryCalls = 0;

        var act = async () => await executor.ExecuteAsync<string>(
            action: () =>
            {
                actionCalls++;
                throw new BiletBankSessionExpiredException($"expired-{actionCalls}");
            },
            recoveryAction: () => { recoveryCalls++; return Task.CompletedTask; });

        await act.Should().ThrowAsync<BiletBankSessionExpiredException>()
            .WithMessage("expired-2");

        actionCalls.Should().Be(2, "max 1 retry, total attempts = initial + 1");
        recoveryCalls.Should().Be(1, "recovery runs exactly once");
    }

    [Fact]
    public async Task ExecuteAsync_does_not_retry_other_exceptions()
    {
        var executor = Build();
        var actionCalls = 0;
        var recoveryCalls = 0;

        var act = async () => await executor.ExecuteAsync<string>(
            action: () =>
            {
                actionCalls++;
                throw new InvalidOperationException("boom");
            },
            recoveryAction: () => { recoveryCalls++; return Task.CompletedTask; });

        await act.Should().ThrowAsync<InvalidOperationException>();
        actionCalls.Should().Be(1);
        recoveryCalls.Should().Be(0, "non-session exceptions must not trigger recovery");
    }
}

public class BiletBankFaultDetectorTests
{
    [Theory]
    [InlineData("Session has expired", true)]
    [InlineData("SESSION TIMEOUT", true)]
    [InlineData("Not authenticated", true)]
    [InlineData("Login required", true)]
    [InlineData("Invalid Token provided", true)]
    [InlineData("Authentication failed", true)]
    [InlineData("Some other error", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsSessionExpiredFault_detects_keywords(string? input, bool expected)
    {
        BiletBankFaultDetector.IsSessionExpiredFault(input).Should().Be(expected);
    }

    [Fact]
    public void IsSessionExpiredError_detects_xml_parse_pattern()
    {
        var ex = new InvalidOperationException("There is an error in XML document (1, 1).");
        BiletBankFaultDetector.IsSessionExpiredError(ex).Should().BeTrue();
    }

    [Fact]
    public void IsSessionExpiredError_walks_inner_exceptions()
    {
        var inner = new Exception("Authentication failed");
        var outer = new Exception("wrapper", inner);
        BiletBankFaultDetector.IsSessionExpiredError(outer).Should().BeTrue();
    }

    [Fact]
    public void IsSessionExpiredStatus_returns_true_for_401_and_403()
    {
        BiletBankFaultDetector.IsSessionExpiredStatus(401).Should().BeTrue();
        BiletBankFaultDetector.IsSessionExpiredStatus(403).Should().BeTrue();
        BiletBankFaultDetector.IsSessionExpiredStatus(500).Should().BeFalse();
        BiletBankFaultDetector.IsSessionExpiredStatus(200).Should().BeFalse();
    }

    [Fact]
    public void ThrowIfSessionExpired_throws_for_session_message()
    {
        var act = () => BiletBankFaultDetector.ThrowIfSessionExpired(true, "Session expired", "Allocate", "sess-1");
        act.Should().Throw<BiletBankSessionExpiredException>()
            .Where(e => e.OperationName == "Allocate" && e.SessionId == "sess-1");
    }

    [Fact]
    public void ThrowIfSessionExpired_noop_when_hasError_false()
    {
        var act = () => BiletBankFaultDetector.ThrowIfSessionExpired(false, "Session expired", "Allocate");
        act.Should().NotThrow();
    }

    [Fact]
    public void ThrowIfSessionExpired_noop_for_unrelated_error()
    {
        var act = () => BiletBankFaultDetector.ThrowIfSessionExpired(true, "Price changed", "Allocate");
        act.Should().NotThrow();
    }
}
