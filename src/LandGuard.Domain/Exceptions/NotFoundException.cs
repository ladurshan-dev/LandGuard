namespace LandGuard.Domain.Exceptions;

/// <summary>
/// Thrown when a lookup by id (or other unique key) finds nothing - e.g.
/// GetPropertyByIdAsync(id) for a property that doesn't exist. Mapped to
/// HTTP 404 by ExceptionHandlingMiddleware in the API layer. Using a
/// dedicated exception type (instead of returning null and having every
/// caller null-check) makes "not found" an explicit, impossible-to-ignore
/// outcome, and keeps controllers free of repetitive null-handling code.
/// </summary>
public class NotFoundException : Exception
{
    public NotFoundException()
        : base()
    {
    }

    public NotFoundException(string message)
        : base(message)
    {
    }

    public NotFoundException(string name, object key)
        : base($"Entity \"{name}\" with key ({key}) was not found.")
    {
    }
}
