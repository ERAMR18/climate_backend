using FluentValidation;

namespace Climate.Identity.Application.Users;

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(request => request.Login).NotEmpty().MaximumLength(254);
        RuleFor(request => request.Password).NotEmpty().MaximumLength(128);
    }
}
