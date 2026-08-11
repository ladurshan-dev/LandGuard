using LandGuard.Application.Common.Interfaces;
using LandGuard.Application.DTOs.GovernmentIdentity;

namespace LandGuard.Infrastructure.Services;

/// <inheritdoc cref="IGovernmentIdentityRegistryService" />
/// <summary>
/// Phase 1 implementation: a small, fixed, fully in-memory set of
/// fictional government IDENTITY records - people/NIC/legal identity,
/// deliberately kept in its own file/class, never merged into
/// <see cref="DummyGovernmentRegistryService"/> (which represents the
/// separate LAND/deed registry - see <see cref="IGovernmentIdentityRegistryService"/>'s
/// own doc comment for why the two must never be confused). No real
/// people's NICs or names.
/// </summary>
public class DummyGovernmentIdentityRegistryService : IGovernmentIdentityRegistryService
{
    /// <summary>
    /// One record per existing synthetic test Seller (see
    /// 05_SeedData.sql's Section 9) plus two standalone demonstration
    /// records not tied to any seeded account - a name-mismatch demo
    /// (NIC exists, but a registrant typing a different name gets
    /// Failed) and a not-found demo (any NIC not in this list at all
    /// already covers "not found" for free, so no explicit record is
    /// needed for that case).
    /// </summary>
    private static readonly IReadOnlyDictionary<string, GovernmentIdentityRecordDto> SeedRecords =
        new List<GovernmentIdentityRecordDto>
        {
            // Matches the 4 synthetic deed-verification test sellers
            // (05_SeedData.sql Section 9) exactly - each expected Verified.
            new() { Nic = "199012345678", FullName = "Nimal Perera", IsActive = true },
            new() { Nic = "199076543210", FullName = "Priya Wickramasinghe", IsActive = true },
            new() { Nic = "199211122233", FullName = "Kasun Rathnayake", IsActive = true },
            new() { Nic = "199355566677", FullName = "Dilani Gunawardena", IsActive = true },

            // Standalone name-mismatch demonstration record: register a new
            // Seller with this NIC but any OTHER name to see Failed.
            new() { Nic = "199499911223", FullName = "Kamal Wijesinghe", IsActive = true },
        }.ToDictionary(r => r.Nic, StringComparer.Ordinal);

    /// <summary>
    /// Manual test hook for the "technical failure" scenario (Part H,
    /// scenario D) - registering a Seller with EXACTLY this NIC makes this
    /// method throw instead of returning, simulating a registry
    /// outage/timeout so SellerIdentityVerificationService's catch block
    /// (-&gt; IdentityStatus.Pending, never Failed) can be exercised
    /// deterministically without a real network dependency to fail.
    ///
    /// CORRECTION (post-review): this MUST be a value that PASSES
    /// AuthValidationRules.NicPattern (<c>^([0-9]{9}[VvXx]|[0-9]{12})$</c>),
    /// not fail it - the required test flow is "registration succeeds ->
    /// account created -> THEN the identity check is attempted and throws",
    /// so the NIC has to clear ordinary registration validation first. The
    /// original "0000000000000" (13 digits) was out-of-format and would
    /// have been rejected by the validator before an account was ever
    /// created, never reaching this service at all. "000000000000" (12
    /// zeros) is a valid 12-digit-format NIC that satisfies the pattern,
    /// clearly synthetic, and does not collide with any seeded record
    /// above or any real-looking NIC, so it can only be reached by a
    /// caller that deliberately typed this exact sentinel value.
    /// </summary>
    public const string SimulatedTechnicalFailureNic = "000000000000";

    public Task<GovernmentIdentityRecordDto?> GetByNicAsync(string nic, CancellationToken cancellationToken = default)
    {
        if (string.Equals(nic, SimulatedTechnicalFailureNic, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Simulated Government Identity Registry outage (test sentinel NIC) - not a real failure.");
        }

        return Task.FromResult(SeedRecords.TryGetValue(nic, out var record) ? record : null);
    }
}
