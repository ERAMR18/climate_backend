using Climate.Sensors.Application.Abstractions;
using Climate.Sensors.Application.Communities;
using Climate.Sensors.Domain.Communities;
using Climate.SharedKernel.Results;
using Moq;

namespace Climate.Sensors.Tests.Communities;

public sealed class CommunityServiceTests
{
    [Fact]
    public async Task CreatePersistsValidatedCommunity()
    {
        var repository = new Mock<ISensorCatalogRepository>();
        Community? persisted = null;
        repository.Setup(value => value.AddCommunityAsync(It.IsAny<Community>(), It.IsAny<CancellationToken>()))
            .Callback<Community, CancellationToken>((community, _) => persisted = community)
            .Returns(Task.CompletedTask);
        var service = new CommunityService(
            repository.Object,
            new CreateCommunityRequestValidator(),
            new UpdateCommunityRequestValidator(),
            TimeProvider.System);

        Result<CommunityResponse> result = await service.CreateAsync(
            new CreateCommunityRequest("Rural community", "Description", 14.6m, -90.5m),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(persisted);
        Assert.True(persisted.IsActive);
    }

    [Fact]
    public async Task CreateRejectsInvalidCoordinates()
    {
        var repository = new Mock<ISensorCatalogRepository>();
        var service = new CommunityService(
            repository.Object,
            new CreateCommunityRequestValidator(),
            new UpdateCommunityRequestValidator(),
            TimeProvider.System);

        Result<CommunityResponse> result = await service.CreateAsync(
            new CreateCommunityRequest("Community", null, 91m, -181m),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }
}
