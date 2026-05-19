using GBILET.Core.Exceptions;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;

namespace GBILET.Infrastructure.Resilience;

/// <summary>
/// BiletBank SOAP cagrilarinda kullanilan Polly retry policy fabrikasi.
/// HttpRequestException, TimeoutException, TaskCanceledException icin 3 deneme + exponential backoff (1s, 2s, 4s).
/// BiletBankSessionExpiredException ve XML parse hatalari retry edilmez.
/// </summary>
public static class BiletBankRetryPolicy
{
    public static IAsyncPolicy<HttpResponseMessage> Build(ILogger logger)
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError() // 5xx, 408
            .Or<TimeoutException>()
            .Or<TaskCanceledException>()
            .Or<HttpRequestException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)),
                onRetry: (outcome, delay, attempt, _) =>
                {
                    logger.LogWarning(
                        "[BiletBankRetry] Transient failure. Attempt {Attempt}, waiting {Delay}ms. Reason: {Reason}",
                        attempt, delay.TotalMilliseconds,
                        outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString());
                });
    }

    /// <summary>
    /// Manual retry policy (HttpClient haricinde, dogrudan kod cagrilarinda kullanilir).
    /// SessionExpired ve XML parse hatasi disinda transient hatalar icin retry uygular.
    /// </summary>
    public static IAsyncPolicy BuildGeneric(ILogger logger)
    {
        return Policy
            .Handle<TimeoutException>()
            .Or<TaskCanceledException>()
            .Or<HttpRequestException>()
            .Or<Exception>(ex =>
                ex is not BiletBankSessionExpiredException
                && !(ex.Message?.Contains("There is an error in XML document", StringComparison.OrdinalIgnoreCase) ?? false))
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)),
                onRetry: (ex, delay, attempt, _) =>
                {
                    logger.LogWarning(
                        "[BiletBankRetryGeneric] Transient failure. Attempt {Attempt}, waiting {Delay}ms. Reason: {Reason}",
                        attempt, delay.TotalMilliseconds, ex.Message);
                });
    }
}
