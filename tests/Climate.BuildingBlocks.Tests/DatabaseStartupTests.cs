using System.Data.Common;
using Climate.Contracts;
using Microsoft.Extensions.Logging.Abstractions;

namespace Climate.BuildingBlocks.Tests;

public sealed class DatabaseStartupTests
{
    [Fact]
    public async Task RetriesDatabaseFailureUntilDependencyIsAvailable()
    {
        int calls = 0;
        await DatabaseStartup.RunAsync(_ => ++calls < 3 ? Task.FromException(new UnavailableException()) : Task.CompletedTask,
            NullLogger.Instance, CancellationToken.None, delay: TimeSpan.Zero);
        Assert.Equal(3, calls);
    }

    [Fact]
    public async Task ExhaustedRetriesFailStartup()
    {
        int calls = 0;
        await Assert.ThrowsAsync<UnavailableException>(() => DatabaseStartup.RunAsync(_ =>
        { calls++; throw new UnavailableException(); }, NullLogger.Instance, CancellationToken.None, 2, TimeSpan.Zero));
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task ConfigurationErrorsAreNotRetried()
    {
        int calls = 0;
        await Assert.ThrowsAsync<InvalidOperationException>(() => DatabaseStartup.RunAsync(_ =>
        { calls++; throw new InvalidOperationException(); }, NullLogger.Instance, CancellationToken.None));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task ShutdownCancelsRetryDelay()
    {
        using var cancellation = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => DatabaseStartup.RunAsync(_ =>
        { cancellation.Cancel(); throw new UnavailableException(); }, NullLogger.Instance, cancellation.Token));
    }

    private sealed class UnavailableException : DbException;
}
