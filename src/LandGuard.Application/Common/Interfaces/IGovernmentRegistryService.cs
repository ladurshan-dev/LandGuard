using LandGuard.Application.DTOs.GovernmentRegistry;

namespace LandGuard.Application.Common.Interfaces;

/// <summary>
/// Abstraction over retrieving a trusted government land/deed record, so
/// Application code never depends on how (or where) the government
/// registry's data is actually sourced - the same Dependency Inversion
/// pattern <see cref="IGeocodingService"/> and <see cref="IOcrService"/>
/// already establish for every other external/trusted concern in this
/// solution. Implemented for Phase 1 by Infrastructure's
/// <c>DummyGovernmentRegistryService</c> against a small, fixed, in-memory
/// dataset, standing in for the real government registry integration this
/// academic project has no access to.
///
/// A government land record is trusted reference data, kept conceptually
/// separate from <c>Property</c> (the seller's own, unverified
/// submission) - nothing behind this interface ever returns a
/// <c>Property</c>/<c>PropertyListingResult</c> shape, reads from, or
/// writes to <c>dbo.Property</c>.
///
/// Each lookup returns null - never throws - when no record matches, the
/// same "not found is a normal, valid outcome" contract
/// <see cref="IGeocodingService.GeocodeAsync"/> uses. A null result is
/// itself meaningful once a later phase's comparison engine is wired up,
/// since a deed with no matching government record at all is exactly the
/// "missing government record" scenario that engine is expected to flag.
///
/// Phase 1 only: this interface exposes lookups, not the comparison
/// itself - matching the seller's OCR-extracted deed data against the
/// record returned here is explicitly left for a later phase.
/// </summary>
public interface IGovernmentRegistryService
{
    /// <summary>Looks up the trusted government record registered against a seller's NIC.</summary>
    Task<GovernmentLandRecordDto?> GetByNicAsync(string nic, CancellationToken cancellationToken = default);

    /// <summary>Looks up the trusted government record for a specific deed number.</summary>
    Task<GovernmentLandRecordDto?> GetByDeedNumberAsync(string deedNumber, CancellationToken cancellationToken = default);

    /// <summary>Looks up the trusted government record for a specific property reference.</summary>
    Task<GovernmentLandRecordDto?> GetByPropertyReferenceAsync(string propertyReference, CancellationToken cancellationToken = default);
}
