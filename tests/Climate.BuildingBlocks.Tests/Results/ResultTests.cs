using Climate.SharedKernel.Results;

namespace Climate.BuildingBlocks.Tests.Results;

public sealed class ResultTests
{
    [Fact]
    public void SuccessCreatesSuccessfulResultWithoutError()
    {
        Result result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(ApplicationError.None, result.Error);
    }

    [Fact]
    public void FailureExposesSpecifiedError()
    {
        ApplicationError error = ApplicationError.NotFound("sensor.not_found", "The sensor was not found.");

        Result result = Result.Failure(error);

        Assert.True(result.IsFailure);
        Assert.Equal(error, result.Error);
    }

    [Fact]
    public void GenericFailureRejectsValueAccess()
    {
        Result<Guid> result = Result.Failure<Guid>(
            ApplicationError.Validation("sensor.invalid", "The sensor identifier is invalid."));

        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void GenericSuccessExposesValue()
    {
        Guid expected = Guid.NewGuid();

        Result<Guid> result = Result.Success(expected);

        Assert.Equal(expected, result.Value);
    }
}
