using LandGuard.Application.Common.Models;
using LandGuard.Application.DTOs.DeedComparison;

namespace LandGuard.Application.Common.Interfaces;

/// <summary>
/// Service Layer contract for the Government Registry module's Phase 4
/// deed comparison. <c>DeedComparisonController</c> depends only on this
/// interface, never on <c>GovernmentDeedComparisonService</c> directly or
/// on any of the OCR/storage/registry abstractions it composes - the same
/// shape every other service in this solution uses.
///
/// Takes the seller's deed as an actual uploaded file, not a JSON object
/// of claimed field values - <see cref="DTOs.DeedComparison.SellerDeedData"/>
/// is built internally, entirely from OCR'ing this upload (plus
/// <c>Property.Price</c>, already legitimately captured), never from
/// caller-supplied "trust me" values. See
/// <see cref="CompareAsync"/>'s parameters.
///
/// Throws <see cref="LandGuard.Domain.Exceptions.NotFoundException"/> if
/// <c>propertyId</c> does not exist (404), or
/// <see cref="UnauthorizedAccessException"/> if the caller is neither the
/// property's owning Seller nor an Admin (403) - the same precise-status-
/// code split <c>PropertyService.DeleteImageAsync</c> already established,
/// deliberately not a generic <see cref="Result"/> failure for these two
/// cases, since "you don't own this property" must never be
/// indistinguishable from an ordinary validation failure. An unresolvable
/// or Cancelled government record is, by contrast, an entirely expected
/// outcome (Scenario F) and is returned as a normal, successful
/// <see cref="Result{T}"/> - not an exception.
/// </summary>
public interface IGovernmentDeedComparisonService
{
    /// <summary>
    /// Runs the full Phase 4 pipeline for one property: verify the caller
    /// owns <paramref name="propertyId"/> (or is an Admin) -&gt; save +
    /// OCR the seller's uploaded deed via the existing
    /// <see cref="IOcrDocumentService"/> -&gt; resolve the trusted
    /// government record via <see cref="IGovernmentRegistryService"/>
    /// -&gt; if found and Active, open + OCR its deed PDF via
    /// <see cref="IFileStorageService.OpenDocumentAsync"/> and the
    /// existing <see cref="IOcrService"/> -&gt; diff both sides with
    /// <c>Services.DeedFieldComparer</c>. If the government record is
    /// missing, has no PDF on file, or is not Active, returns a
    /// <see cref="DTOs.DeedComparison.GovernmentDeedComparisonReport"/>
    /// with <c>OverallOutcome = "MissingOrCancelledGovernmentRecord"</c>
    /// without attempting government PDF OCR.
    /// </summary>
    /// <param name="propertyId">The listing the uploaded deed is claimed to belong to.</param>
    /// <param name="fileName">The uploaded file's original name (extension only - never trusted as a storage path).</param>
    /// <param name="contentType">The uploaded file's content type, validated against the same allow-list <see cref="IOcrDocumentService"/> already enforces.</param>
    /// <param name="sellerDeedContent">The seller's actual deed file content - the sole source of every OCR-derived <see cref="DTOs.DeedComparison.SellerDeedData"/> field.</param>
    /// <param name="callerId">The authenticated caller's own user id (never trusted from the request body).</param>
    /// <param name="callerRole">The authenticated caller's role claim.</param>
    Task<Result<GovernmentDeedComparisonReport>> CompareAsync(
        int propertyId,
        string fileName,
        string contentType,
        Stream sellerDeedContent,
        int callerId,
        string? callerRole,
        CancellationToken cancellationToken = default);
}
