using System.Text.Json;
using Climate.Audit.Api;
using Climate.Audit.Application.Auditing;
using Climate.Contracts.Audit;
using Climate.SharedKernel.Results;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Climate.Audit.Tests.Auditing;

public sealed class AuditConsumerTests
{
    [Theory]
    [InlineData(0, false, true, 1)]
    [InlineData(1, false, true, 2)]
    [InlineData(9, false, false, 3)]
    [InlineData(0, true, false, 1)]
    public async Task RetryIsBoundedAndValidationFailureIsNotRetried(int failures, bool invalid, bool accepted, int attempts)
    {
        var service = new StubService(failures, invalid);
        await using var provider = new ServiceCollection().AddSingleton<IAuditService>(service).BuildServiceProvider();
        using var consumer = new AuditConsumer(new RabbitMqAuditOptions(), provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<AuditConsumer>.Instance);
        var message = new AuditLogRequested(Guid.NewGuid(), DateTimeOffset.UtcNow, "trace", Guid.NewGuid(), "admin", "Update", "Sensor", "42", "Updated", null);
        Assert.Equal(accepted, await consumer.PersistAsync(JsonSerializer.SerializeToUtf8Bytes(message), CancellationToken.None));
        Assert.Equal(attempts, service.Attempts);
    }

    [Fact]
    public async Task MalformedJsonIsRejectedBeforePersistence()
    {
        var service = new StubService(0, false);
        await using var provider = new ServiceCollection().AddSingleton<IAuditService>(service).BuildServiceProvider();
        using var consumer = new AuditConsumer(new RabbitMqAuditOptions(), provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<AuditConsumer>.Instance);
        Assert.False(await consumer.PersistAsync("{"u8.ToArray(), CancellationToken.None));
        Assert.Equal(0, service.Attempts);
    }

    private sealed class StubService(int failures, bool invalid) : IAuditService
    {
        public int Attempts { get; private set; }
        public Task<Result<AuditResponse>> RecordAsync(RecordAuditRequest request, CancellationToken cancellationToken)
        {
            if (++Attempts <= failures) throw new IOException("Transient persistence failure");
            return Task.FromResult(invalid ? Result.Failure<AuditResponse>(AuditErrors.Invalid) : Result.Success(new AuditResponse(
                request.EventId, request.UserId, request.UserName, request.Action, request.Resource, request.ResourceId, request.Description, request.IpAddress, request.Timestamp)));
        }
        public Task<IReadOnlyCollection<AuditResponse>> ListAsync(AuditFilter filter, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result<AuditResponse>> GetAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
