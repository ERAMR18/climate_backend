using Climate.SharedKernel.Results;

namespace Climate.Identity.Application.Users;

public static class UserErrors
{
    public static readonly ApplicationError InvalidCredentials = ApplicationError.Unauthorized(
        "auth.invalid_credentials",
        "The supplied credentials are invalid.");

    public static readonly ApplicationError Inactive = ApplicationError.Forbidden(
        "auth.inactive_user",
        "The user account is inactive.");

    public static readonly ApplicationError NotFound = ApplicationError.NotFound(
        "user.not_found",
        "The requested user does not exist.");

    public static readonly ApplicationError UsernameConflict = ApplicationError.Conflict(
        "user.username_conflict",
        "The username is already registered.");

    public static readonly ApplicationError EmailConflict = ApplicationError.Conflict(
        "user.email_conflict",
        "The email is already registered.");
}
