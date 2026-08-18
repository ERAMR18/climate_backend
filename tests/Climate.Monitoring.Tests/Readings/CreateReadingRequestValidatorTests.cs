using Climate.Monitoring.Application.Readings;

namespace Climate.Monitoring.Tests.Readings;

public sealed class CreateReadingRequestValidatorTests
{
    [Fact]
    public async Task ValidatorRejectsEmptySensorIdentifier()
    {
        var validator = new CreateReadingRequestValidator();
        var result = await validator.ValidateAsync(new CreateReadingRequest(Guid.Empty, 10m), CancellationToken.None);
        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ValidatorRejectsFutureTimestamp()
    {
        var validator = new CreateReadingRequestValidator();
        var result = await validator.ValidateAsync(
            new CreateReadingRequest(Guid.NewGuid(), 10m, DateTimeOffset.UtcNow.AddHours(1)),
            CancellationToken.None);
        Assert.False(result.IsValid);
    }
}
