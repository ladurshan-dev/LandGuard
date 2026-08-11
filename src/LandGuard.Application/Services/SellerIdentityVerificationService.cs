using System.Text.RegularExpressions;
using LandGuard.Application.Common.Interfaces;
using LandGuard.Application.Common.Interfaces.StoredProcedures;
using LandGuard.Domain.Enums;

namespace LandGuard.Application.Services;

/// <summary>
/// Implements <see cref="ISellerIdentityVerificationService"/>. Contains no
/// SQL and no HTTP of its own - it composes <see cref="IUserStoredProcedures"/>
/// (read the Seller's own Name/NIC, then persist the verdict) and
/// <see cref="IGovernmentIdentityRegistryService"/> (the trusted lookup),
/// the same "no business logic in Infrastructure" shape every other
/// Application service in this solution already follows.
/// </summary>
public class SellerIdentityVerificationService : ISellerIdentityVerificationService
{
    private readonly IUserStoredProcedures _userStoredProcedures;
    private readonly IGovernmentIdentityRegistryService _identityRegistryService;

    public SellerIdentityVerificationService(
        IUserStoredProcedures userStoredProcedures,
        IGovernmentIdentityRegistryService identityRegistryService)
    {
        _userStoredProcedures = userStoredProcedures;
        _identityRegistryService = identityRegistryService;
    }

    public async Task<IdentityStatus> VerifyAsync(int sellerId, CancellationToken cancellationToken = default)
    {
        var profile = await _userStoredProcedures.GetByIdAsync(sellerId, cancellationToken);

        // Defensive only - AuthService only ever calls this for a Seller it
        // just created/looked up itself, and the reverify endpoint is
        // gated to the Seller role at the controller. A null/non-Seller
        // profile here would mean a caller bug, not a real identity
        // outcome, so nothing is persisted and Pending is returned rather
        // than throwing into the middle of a registration response.
        if (profile is null || !string.Equals(profile.Role, "Seller", StringComparison.Ordinal))
        {
            return IdentityStatus.Pending;
        }

        IdentityStatus status;

        try
        {
            var normalizedNic = NormalizeNic(profile.Nic);
            var record = normalizedNic is null
                ? null
                : await _identityRegistryService.GetByNicAsync(normalizedNic, cancellationToken);

            status = Classify(record, profile.Name);
        }
        catch
        {
            // A genuine technical failure (registry unavailable, timeout,
            // unexpected exception - see IGovernmentIdentityRegistryService's
            // own doc comment) must never accuse the Seller of a
            // name/NIC mismatch. Pending, not Failed - retryable once the
            // registry recovers.
            status = IdentityStatus.Pending;
        }

        await _userStoredProcedures.SetIdentityStatusAsync(sellerId, status.ToString(), cancellationToken);

        return status;
    }

    /// <summary>
    /// Identity Matching Rule: NIC lookup is authoritative.
    ///   1. No registry record for this NIC -&gt; Failed.
    ///   2. Registry record found but not active -&gt; Failed.
    ///   3. Registry record found, active, but its normalized full name
    ///      differs from the Seller's own normalized name -&gt; Failed.
    ///   4. NIC + normalized name match -&gt; Verified.
    /// No fuzzy matching - this project has no existing, well-defined
    /// fuzzy-match convention to reuse, and inventing one here risks
    /// silently accepting a genuine mismatch.
    /// </summary>
    private static IdentityStatus Classify(DTOs.GovernmentIdentity.GovernmentIdentityRecordDto? record, string sellerName)
    {
        if (record is null || !record.IsActive)
        {
            return IdentityStatus.Failed;
        }

        return NormalizeName(record.FullName) == NormalizeName(sellerName)
            ? IdentityStatus.Verified
            : IdentityStatus.Failed;
    }

    /// <summary>Trim + collapse repeated internal whitespace + case-insensitive (ordinal, upper-invariant) - exactly the three rules this requirement specifies, nothing more.</summary>
    private static string NormalizeName(string name)
    {
        var collapsed = Regex.Replace(name.Trim(), @"\s+", " ");
        return collapsed.ToUpperInvariant();
    }

    /// <summary>Trim only - NIC format itself is already validated at registration (AuthValidationRules.NicPattern); this just guards against stray whitespace before a dictionary/lookup comparison. Null/blank NIC (should not happen for a Seller - NIC is mandatory - but defensive) yields no lookup at all, which Classify already treats as Failed via a null record.</summary>
    private static string? NormalizeNic(string? nic) =>
        string.IsNullOrWhiteSpace(nic) ? null : nic.Trim();
}
