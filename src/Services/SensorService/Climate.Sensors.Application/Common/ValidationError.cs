using Climate.SharedKernel.Results;
using FluentValidation.Results;

namespace Climate.Sensors.Application.Common;

internal static class ValidationError
{
    public static ApplicationError From(ValidationResult validationResult) =>
        ApplicationError.Validation(
            "validation.failed",
            string.Join(" ", validationResult.Errors.Select(error => error.ErrorMessage).Distinct()));
}
