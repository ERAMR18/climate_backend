namespace Climate.SharedKernel.Results;

public sealed record ApplicationError(string Code, string Description, ErrorType Type)
{
    public static readonly ApplicationError None = new(string.Empty, string.Empty, ErrorType.Failure);

    public static ApplicationError Failure(string code, string description) =>
        new(code, description, ErrorType.Failure);

    public static ApplicationError Validation(string code, string description) =>
        new(code, description, ErrorType.Validation);

    public static ApplicationError NotFound(string code, string description) =>
        new(code, description, ErrorType.NotFound);

    public static ApplicationError Conflict(string code, string description) =>
        new(code, description, ErrorType.Conflict);

    public static ApplicationError Unauthorized(string code, string description) =>
        new(code, description, ErrorType.Unauthorized);

    public static ApplicationError Forbidden(string code, string description) =>
        new(code, description, ErrorType.Forbidden);
}
