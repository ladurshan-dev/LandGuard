namespace LandGuard.Application.Common.Interfaces;

/// <summary>
/// Abstraction over system time. Several upcoming fraud rules are
/// time-sensitive (e.g. "Seller History": how many listings this Seller
/// submitted in the last 30 days; "Price Anomaly": comparison against
/// recent registry sale dates), and directly calling DateTime.UtcNow
/// inside those rules would make them impossible to unit test
/// deterministically. Injecting IDateTimeService lets tests supply a
/// fixed clock instead.
/// </summary>
public interface IDateTimeService
{
    DateTime UtcNow { get; }
}
