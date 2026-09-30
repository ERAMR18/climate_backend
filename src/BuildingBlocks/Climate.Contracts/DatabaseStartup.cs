using System.Data.Common;
using Microsoft.Extensions.Logging;

namespace Climate.Contracts;

public static partial class DatabaseStartup
{
    // Retry initialization with a fresh scope, rather than replaying HTTP writes.
    public static async Task RunAsync(Func<CancellationToken, Task> initialize, ILogger logger,
        CancellationToken token, int attempts = 12, TimeSpan? delay = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(attempts, 1);
        for (int attempt = 1; ; attempt++)
        {
            token.ThrowIfCancellationRequested();
            try { await initialize(token); return; }
            catch (DbException) when (attempt < attempts)
            {
                Waiting(logger, attempt, attempts);
                await Task.Delay(delay ?? TimeSpan.FromSeconds(5), token);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Database initialization unavailable, attempt {Attempt}/{Attempts}; retrying")]
    private static partial void Waiting(ILogger logger, int attempt, int attempts);
}
