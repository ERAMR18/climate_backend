using Climate.Sensors.Application.Abstractions;
using Climate.Sensors.Application.Communities;
using Climate.Sensors.Application.Sensors;
using Climate.Sensors.Domain.Communities;
using Climate.Sensors.Domain.Sensors;
using Climate.SharedKernel.Results;
using Moq;

namespace Climate.Sensors.Tests.Sensors;

public sealed class SensorServiceTests
{
    private readonly Mock<ISensorCatalogRepository> _repository = new();

    [Fact]
    public async Task CreatePersistsActiveSensorForExistingCommunity()
    {
        Community community = CreateCommunity();
        _repository.Setup(repository => repository.GetCommunityAsync(community.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(community);
        Sensor? persisted = null;
        _repository.Setup(repository => repository.AddSensorAsync(It.IsAny<Sensor>(), It.IsAny<CancellationToken>()))
            .Callback<Sensor, CancellationToken>((sensor, _) => persisted = sensor)
            .Returns(Task.CompletedTask);

        Result<SensorResponse> result = await CreateService().CreateAsync(
            CreateRequest(community.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(persisted);
        Assert.True(persisted.IsActive);
        Assert.Equal("TEMP-001", persisted.NormalizedCode);
        Assert.Equal(community.Name, result.Value.CommunityName);
        _repository.Verify(repository => repository.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateRejectsDuplicateCode()
    {
        _repository.Setup(repository => repository.SensorCodeExistsAsync(
                "TEMP-001",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Result<SensorResponse> result = await CreateService().CreateAsync(
            CreateRequest(Guid.NewGuid()),
            CancellationToken.None);

        Assert.Equal(SensorErrors.CodeConflict, result.Error);
        _repository.Verify(
            repository => repository.AddSensorAsync(It.IsAny<Sensor>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DeactivateUsesLogicalDeletion()
    {
        Sensor sensor = CreateSensor(CreateCommunity().Id);
        _repository.Setup(repository => repository.GetSensorAsync(sensor.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sensor);

        Result result = await CreateService().SetStatusAsync(sensor.Id, false, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(sensor.IsActive);
        _repository.Verify(repository => repository.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ActivateRejectsSensorFromInactiveCommunity()
    {
        Community community = CreateCommunity();
        community.Update(community.Name, community.Description, community.Latitude, community.Longitude, false);
        Sensor sensor = CreateSensor(community.Id);
        sensor.SetStatus(false, DateTimeOffset.UtcNow);
        _repository.Setup(repository => repository.GetSensorAsync(sensor.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sensor);
        _repository.Setup(repository => repository.GetCommunityAsync(community.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(community);

        Result result = await CreateService().SetStatusAsync(sensor.Id, true, CancellationToken.None);

        Assert.Equal(CommunityErrors.Inactive, result.Error);
        Assert.False(sensor.IsActive);
    }

    private SensorService CreateService() =>
        new(
            _repository.Object,
            new CreateSensorRequestValidator(),
            new UpdateSensorRequestValidator(),
            TimeProvider.System);

    private static CreateSensorRequest CreateRequest(Guid communityId) =>
        new("Temperature", "TEMP-001", null, SensorType.Temperature, "°C", communityId, 14.6m, -90.5m);

    private static Community CreateCommunity() =>
        Community.Create(Guid.NewGuid(), "Community", null, 14.6m, -90.5m, DateTimeOffset.UtcNow);

    private static Sensor CreateSensor(Guid communityId) =>
        Sensor.Create(
            Guid.NewGuid(),
            "Temperature",
            "TEMP-001",
            null,
            SensorType.Temperature,
            "°C",
            communityId,
            14.6m,
            -90.5m,
            DateTimeOffset.UtcNow);
}
