namespace LandGuard.Application.Common.Models;

/// <summary>
/// Standard envelope for Service Layer outcomes. Services return a
/// Result/Result&lt;T&gt; instead of throwing for *expected* business
/// outcomes (e.g. "email already registered", "listing not eligible for
/// re-submission") and reserve exceptions (NotFoundException,
/// DomainException, ValidationException) for conditions that are truly
/// exceptional. Controllers translate a failed Result into a 400-level
/// response with the Errors list, giving every endpoint - across Auth,
/// Property, Fraud Report and Admin modules - the same response shape
/// without each controller reinventing it.
/// </summary>
public class Result
{
    public bool Succeeded { get; }

    public IReadOnlyList<string> Errors { get; }

    protected Result(bool succeeded, IEnumerable<string> errors)
    {
        Succeeded = succeeded;
        Errors = errors.ToList().AsReadOnly();
    }

    public static Result Success() => new(true, Array.Empty<string>());

    public static Result Failure(IEnumerable<string> errors) => new(false, errors);

    public static Result Failure(string error) => new(false, new[] { error });
}

/// <summary>
/// Result variant that also carries a return value on success (e.g. the
/// created PropertyDto, or a generated FraudReportDto). Data is null
/// whenever Succeeded is false - callers should always check Succeeded
/// before reading Data.
/// </summary>
public class Result<T> : Result
{
    public T? Data { get; }

    protected Result(bool succeeded, T? data, IEnumerable<string> errors)
        : base(succeeded, errors)
    {
        Data = data;
    }

    public static Result<T> Success(T data) => new(true, data, Array.Empty<string>());

    public new static Result<T> Failure(IEnumerable<string> errors) => new(false, default, errors);

    public new static Result<T> Failure(string error) => new(false, default, new[] { error });
}
