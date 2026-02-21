using System;
using System.Threading.Tasks;

namespace AgenticAI.Core.Utilities
{
    /// <summary>
    /// Simple retry helper with exponential backoff.
    /// Use for transient operations (web requests, flaky UI actions).
    /// </summary>
    public static class RetryHelper
    {
        public static async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> action, int maxRetries = 3, int initialDelayMs = 500, Func<Exception, int, Task>? onRetry = null)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            var attempt = 0;
            var delay = initialDelayMs;

            while (true)
            {
                try
                {
                    return await action().ConfigureAwait(false);
                }
                catch (Exception ex) when (attempt < maxRetries)
                {
                    attempt++;
                    if (onRetry != null)
                    {
                        try { await onRetry(ex, attempt).ConfigureAwait(false); } catch { }
                    }

                    await Task.Delay(delay).ConfigureAwait(false);
                    delay *= 2; // exponential backoff
                }
            }
        }

        public static async Task ExecuteWithRetryAsync(Func<Task> action, int maxRetries = 3, int initialDelayMs = 500, Func<Exception,int,Task>? onRetry = null)
        {
            await ExecuteWithRetryAsync(async () => { await action().ConfigureAwait(false); return true; }, maxRetries, initialDelayMs, async (ex, attempt) => { if (onRetry != null) await onRetry(ex, attempt).ConfigureAwait(false); }).ConfigureAwait(false);
        }
    }
}
