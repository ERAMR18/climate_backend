using Climate.Contracts.Alerts;
using Climate.Events.Application.Abstractions;
using Climate.Events.Application.Events;
using Climate.Events.Domain.Events;

namespace Climate.Events.Tests.Events;

public sealed class ClimateEventServiceTests
{
    [Fact]
    public async Task RecordCreatesEventWithTimestamp()
    {
        var repository = new FakeRepository();
        RecordClimateEventRequest request = CreateRequest();
        var result = await new ClimateEventService(repository).RecordAsync(request, CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal(request.OccurredAt, result.Value.OccurredAt);
        Assert.Single(repository.Items);
    }

    [Fact]
    public async Task RecordIsIdempotentByAlertIdAndUpdatesResolution()
    {
        var repository = new FakeRepository();
        RecordClimateEventRequest request = CreateRequest();
        var service = new ClimateEventService(repository);
        await service.RecordAsync(request, CancellationToken.None);
        DateTimeOffset resolved = request.OccurredAt.AddHours(1);
        await service.RecordAsync(request with { EventId = Guid.NewGuid(), AlertLevel = AlertLevel.Red, ResolvedAt = resolved }, CancellationToken.None);
        Assert.Single(repository.Items);
        Assert.Equal(resolved, repository.Items[0].ResolvedAt);
        Assert.Equal(AlertLevel.Red, repository.Items[0].AlertLevel);
    }

    [Fact]
    public async Task GetUnknownEventReturnsNotFound()
    {
        var result = await new ClimateEventService(new FakeRepository()).GetByIdAsync(Guid.NewGuid(), CancellationToken.None);
        Assert.Equal(EventErrors.NotFound, result.Error);
    }

    [Fact]
    public async Task InvalidRequestIsRejected()
    {
        RecordClimateEventRequest request = CreateRequest() with { AlertId = Guid.Empty };
        var result = await new ClimateEventService(new FakeRepository()).RecordAsync(request, CancellationToken.None);
        Assert.Equal(EventErrors.Invalid, result.Error);
    }

    [Fact]
    public async Task ListPassesEveryFilterToRepository()
    {
        var repository = new FakeRepository();
        DateTimeOffset from = DateTimeOffset.UtcNow.AddDays(-1);
        DateTimeOffset to = DateTimeOffset.UtcNow;
        await new ClimateEventService(repository).ListAsync(new(RiskType.Flood, AlertLevel.Red, Guid.NewGuid(), Guid.NewGuid(), from, to), CancellationToken.None);
        Assert.True(repository.ListWasCalled);
    }

    private static RecordClimateEventRequest CreateRequest() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
        Guid.NewGuid(), RiskType.Flood, AlertLevel.Orange, "Flood threshold exceeded.", DateTimeOffset.UtcNow, null);

    private sealed class FakeRepository : IClimateEventRepository
    {
        public List<ClimateEvent> Items { get; } = [];
        public bool ListWasCalled { get; private set; }
        public Task<ClimateEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(Items.FirstOrDefault(x => x.Id == id));
        public Task<ClimateEvent?> GetByAlertIdAsync(Guid alertId, CancellationToken cancellationToken) => Task.FromResult(Items.FirstOrDefault(x => x.AlertId == alertId));
        public Task<IReadOnlyCollection<ClimateEvent>> ListAsync(RiskType? riskType, AlertLevel? level, Guid? sensorId,
            Guid? communityId, DateTimeOffset? from, DateTimeOffset? until, CancellationToken cancellationToken)
        { ListWasCalled = true; return Task.FromResult<IReadOnlyCollection<ClimateEvent>>(Items); }
        public Task AddAsync(ClimateEvent climateEvent, CancellationToken cancellationToken) { Items.Add(climateEvent); return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
