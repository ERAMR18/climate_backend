namespace Climate.SharedKernel.Results;

public class Result
{
    protected Result(bool isSuccess, ApplicationError error)
    {
        if (isSuccess && error != ApplicationError.None)
        {
            throw new ArgumentException("A successful result cannot contain an error.", nameof(error));
        }

        if (!isSuccess && error == ApplicationError.None)
        {
            throw new ArgumentException("A failed result must contain an error.", nameof(error));
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public ApplicationError Error { get; }

    public static Result Success() => new(true, ApplicationError.None);

    public static Result Failure(ApplicationError error) => new(false, error);

    public static Result<TValue> Success<TValue>(TValue value) =>
        new(value, true, ApplicationError.None);

    public static Result<TValue> Failure<TValue>(ApplicationError error) =>
        new(default, false, error);
}
