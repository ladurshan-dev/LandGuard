namespace LandGuard.Application.DTOs.GovernmentIdentity;

/// <summary>
/// One trusted record from the (dummy, Phase 1) Government IDENTITY
/// Registry - people/NIC/legal identity. Deliberately a separate concept
/// from <c>GovernmentLandRecordDto</c> (deeds/land/registered ownership -
/// see that DTO's own doc comment): a person's legal identity and a
/// parcel of land are two different authorities in real life, and nothing
/// here is ever compared against <c>Property</c> or a deed - only against
/// a Seller's own account Name/NIC (see <c>SellerIdentityVerificationService</c>).
/// </summary>
public class GovernmentIdentityRecordDto
{
    public string Nic { get; set; } = null!;

    public string FullName { get; set; } = null!;

    /// <summary>False for a record the registry itself no longer considers current (e.g. a cancelled national ID) - treated the same as "no record" by SellerIdentityVerificationService (Failed).</summary>
    public bool IsActive { get; set; } = true;
}
