using Climate.Contracts.Identity;
using FluentValidation;

namespace Climate.Identity.Application.Users;

public sealed class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(request => request.Username)
            .NotEmpty()
            .MinimumLength(3)
            .MaximumLength(50)
            .Matches("^[a-zA-Z0-9._-]+$");

        RuleFor(request => request.Email).NotEmpty().MaximumLength(254).EmailAddress();
        RuleFor(request => request.Role)
            .Must(SystemRoles.IsDefined)
            .WithMessage("Role must be Administrator, Operator or Viewer.");
    }
}
