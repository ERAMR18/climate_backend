using FluentValidation;

namespace Climate.Sensors.Application.Communities;

public sealed class CreateCommunityRequestValidator : AbstractValidator<CreateCommunityRequest>
{
    public CreateCommunityRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().MaximumLength(120);
        RuleFor(request => request.Description).MaximumLength(500);
        RuleFor(request => request.Municipality).MaximumLength(120);
        RuleFor(request => request.Department).MaximumLength(120);
        RuleFor(request => request.Country).MaximumLength(120);
        RuleFor(request => request.Latitude).InclusiveBetween(-90, 90);
        RuleFor(request => request.Longitude).InclusiveBetween(-180, 180);
    }
}

public sealed class UpdateCommunityRequestValidator : AbstractValidator<UpdateCommunityRequest>
{
    public UpdateCommunityRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().MaximumLength(120);
        RuleFor(request => request.Description).MaximumLength(500);
        RuleFor(request => request.Municipality).MaximumLength(120);
        RuleFor(request => request.Department).MaximumLength(120);
        RuleFor(request => request.Country).MaximumLength(120);
        RuleFor(request => request.Latitude).InclusiveBetween(-90, 90);
        RuleFor(request => request.Longitude).InclusiveBetween(-180, 180);
    }
}
