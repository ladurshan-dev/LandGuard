using LandGuard.Application.DTOs.GovernmentIdentity;

namespace LandGuard.Application.Common.Interfaces;

/// <summary>
/// Abstraction over the Government IDENTITY Registry - people/NIC/legal
/// identity - kept structurally separate from
/// <see cref="IGovernmentRegistryService"/> (deeds/land/registered
/// ownership) even though both are "a government lookup" in spirit: the
/// two represent different real-world authorities, and this project must
/// never let a Seller's own identity check quietly reuse (or be confused
/// with) the land-registry deed comparison - see each DTO's own doc
/// comment for the full reasoning.
///
/// Unlike <see cref="IGovernmentRegistryService"/> (which returns null and
/// never throws when no record matches), a genuine TECHNICAL failure here
/// (registry unavailable, timeout, unexpected exception) IS allowed to
/// throw - <see cref="Services.SellerIdentityVerificationService"/> is the
/// one place that catches it and classifies it as
/// <see cref="Enums.IdentityStatus.Pending"/>, never
/// <see cref="Enums.IdentityStatus.Failed"/> (a technical failure must
/// never accuse the Seller of a name/NIC mismatch - see that service's own
/// doc comment). A null return (no exception) means the registry was
/// queried successfully and found nothing for that NIC - a genuine,
/// authoritative negative answer, the same "not found is a normal, valid
/// outcome" contract <see cref="IGeocodingService.GeocodeAsync"/> and
/// <see cref="IGovernmentRegistryService"/> already establish.
/// </summary>
public interface IGovernmentIdentityRegistryService
{
    /// <summary>Looks up the trusted government identity record for a Sri Lankan NIC. Null if the registry has no record for it (not a technical failure - see this interface's own doc comment).</summary>
    Task<GovernmentIdentityRecordDto?> GetByNicAsync(string nic, CancellationToken cancellationToken = default);
}
