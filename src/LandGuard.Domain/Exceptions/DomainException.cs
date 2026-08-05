namespace LandGuard.Domain.Exceptions;

/// <summary>
/// Signals that a business/domain rule was violated - something that is
/// invalid according to LandGuard's rules even though the request was
/// well-formed (e.g. "a Seller cannot upload a property document after
/// the listing has already been RejectedFraudulent"). Distinct from
/// FluentValidation's ValidationException, which covers input shape/
/// format errors, and from NotFoundException, which covers missing
/// entities. Mapped to HTTP 422 Unprocessable Entity by
/// ExceptionHandlingMiddleware.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message)
        : base(message)
    {
    }

    public DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
