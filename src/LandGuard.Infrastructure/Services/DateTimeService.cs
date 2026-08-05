using LandGuard.Application.Common.Interfaces;

namespace LandGuard.Infrastructure.Services;

/// <inheritdoc cref="IDateTimeService" />
public class DateTimeService : IDateTimeService
{
    public DateTime UtcNow => DateTime.UtcNow;
}
