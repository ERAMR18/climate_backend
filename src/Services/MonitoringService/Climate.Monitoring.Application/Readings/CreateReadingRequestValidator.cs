using FluentValidation;

namespace Climate.Monitoring.Application.Readings;

public sealed class CreateReadingRequestValidator : AbstractValidator<CreateReadingRequest>
{
    public CreateReadingRequestValidator()
    {
        RuleFor(request => request.SensorId).NotEmpty();
        RuleFor(request => request.Value).InclusiveBetween(-1_000_000m, 1_000_000m);
        RuleFor(request => request.RecordedAt)
            .Must(value => value is null || value <= DateTimeOffset.UtcNow.AddMinutes(1))
            .WithMessage("RecordedAt cannot be in the future.");
    }
}
