using GBILET.Core.Exceptions;
using Microsoft.Extensions.Logging;

namespace GBILET.Infrastructure.Resilience;

/// <summary>
/// BiletBank session expire durumlarinda recovery action (yeni search ile session yenileme)
/// calistirip islemi sinirli sayida tekrar deneyen generic wrapper.
/// Polly disinda tutulmustur cunku session expire sadece yeniden deneme degil,
/// yeni session uretimi de gerektirir.
/// </summary>
public class SessionRecoveryExecutor
{
    private readonly ILogger<SessionRecoveryExecutor> _logger;

    public SessionRecoveryExecutor(ILogger<SessionRecoveryExecutor> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// action calistirir; BiletBankSessionExpiredException atilirsa recoveryAction calistirip
    /// action'i bir kez yeniden dener. Tek retry, sonsuz dongu yok.
    /// </summary>
    public async Task<T> ExecuteAsync<T>(
        Func<Task<T>> action,
        Func<Task> recoveryAction,
        int maxRetries = 1,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(recoveryAction);

        var attempt = 0;
        while (true)
        {
            try
            {
                return await action().ConfigureAwait(false);
            }
            catch (BiletBankSessionExpiredException ex) when (attempt < maxRetries)
            {
                attempt++;
                _logger.LogWarning(
                    "BiletBank session expired (attempt {Attempt}/{Max}). Operation={Operation}. Recovering...",
                    attempt, maxRetries, ex.OperationName);

                await recoveryAction().ConfigureAwait(false);
            }
        }
    }
}
