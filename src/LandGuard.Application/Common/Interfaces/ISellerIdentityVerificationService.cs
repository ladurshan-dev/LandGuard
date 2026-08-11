using LandGuard.Domain.Enums;

namespace LandGuard.Application.Common.Interfaces;

/// <summary>
/// Seller Government Identity Verification requirement. Answers exactly
/// one question - "is this Seller a verified person?" - by comparing the
/// Seller's OWN account Name/NIC (never a Property's OwnerName/OwnerNIC -
/// those are a different, later, separate question; see
/// <c>FormDeedComparer</c>'s own doc comment for that distinction) against
/// <see cref="IGovernmentIdentityRegistryService"/>. Called once right
/// after a Seller successfully registers (<c>AuthService.RegisterSellerAsync</c>)
/// and again from the Seller-authenticated reverify endpoint - the exact
/// same classification logic either way, so a fresh registration and a
/// manual retry can never disagree about what "Verified" means.
/// </summary>
public interface ISellerIdentityVerificationService
{
    /// <summary>
    /// Looks up the Seller's current Name/NIC, classifies against the
    /// Government Identity Registry, persists the result via
    /// <c>IUserStoredProcedures.SetIdentityStatusAsync</c>, and returns the
    /// resulting status. Never throws for a technical registry failure -
    /// that is caught internally and classified as
    /// <see cref="IdentityStatus.Pending"/>.
    /// </summary>
    Task<IdentityStatus> VerifyAsync(int sellerId, CancellationToken cancellationToken = default);
}
