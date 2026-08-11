using LandGuard.Application.Common.Models;
using LandGuard.Application.DTOs.Property;

namespace LandGuard.Application.Common.Interfaces;

/// <summary>
/// Service Layer contract for Property CRUD, image upload and the
/// seller/public visibility rules around them. PropertyController depends
/// only on this interface, never on PropertyService directly or on any of
/// the stored-procedure/geocoding/file-storage abstractions it composes -
/// the same shape IAuthService established in Module 3.
///
/// Every method returns a <see cref="Result"/>/<see cref="Result{T}"/> for
/// expected outcomes (not found, not the owner, listing not public yet);
/// genuinely exceptional conditions (a database constraint violation, an
/// ownership check the stored procedure itself rejects) surface as
/// exceptions and are translated by <c>ExceptionHandlingMiddleware</c>.
/// </summary>
public interface IPropertyService
{
    /// <summary>Creates a listing owned by <paramref name="sellerId"/>. Geocodes Location/District when Latitude/Longitude are not supplied.</summary>
    Task<Result<PropertyListingResult>> CreateAsync(
        CreatePropertyRequest request, int sellerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads one photo for a listing the caller owns (or any listing, for
    /// an Admin - usp_PropertyImage_Add has no ownership check of its own,
    /// unlike Update/Delete, so this is enforced entirely at this layer),
    /// stores it, attaches it via usp_PropertyImage_Add, then re-runs the
    /// fraud engine so the image-dependent rules (Duplicate Image, Missing
    /// Information) see it. Returns the refreshed <see cref="PropertyDetail"/>.
    /// </summary>
    Task<Result<PropertyDetail>> AddImageAsync(
        int propertyId,
        string fileName,
        string contentType,
        Stream content,
        bool isPrimary,
        int callerId,
        string? callerRole,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets one listing with its images and fraud report. Visible to
    /// anyone once Status is "Approved"; otherwise only to the owning
    /// seller or an Admin - a Buyer probing a Pending/Flagged/Rejected id
    /// gets the same "not found" <see cref="Result"/> as a nonexistent id,
    /// never a distinguishing 403.
    /// </summary>
    Task<Result<PropertyDetail>> GetByIdAsync(
        int propertyId, int? callerId, string? callerRole, CancellationToken cancellationToken = default);

    /// <summary>
    /// Public search over published (Approved, active-seller) listings
    /// only - FR10. <paramref name="callerRole"/> is used only for the
    /// Buyer-privacy redaction below, never for visibility (every caller,
    /// including anonymous, already only ever sees Approved listings here -
    /// see usp_Property_Search/vw_PublishedProperty).
    ///
    /// Buyer privacy requirement: internal fraud-engine output
    /// (RiskScore/RiskLevel/FraudStatus/RiskSummary/RiskGeneratedDate,
    /// and the ability to filter/sort by risk) must never reach a
    /// non-Admin caller, even for an Approved listing - Approval is
    /// sufficient information for a Buyer. This method strips those
    /// fields (and forces the risk-based filter/sort off) for every
    /// caller except an Admin before returning.
    /// </summary>
    Task<Result<PropertySearchResponse>> SearchAsync(
        PropertySearchRequest request, string? callerRole, CancellationToken cancellationToken = default);

    /// <summary>
    /// The seller dashboard grid (FR08) - every status, not just Approved.
    /// Only the seller themselves or an Admin may request it.
    /// </summary>
    Task<Result<IReadOnlyList<PropertyListingResult>>> GetBySellerAsync(
        int sellerId, int callerId, string? callerRole, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a listing the caller owns. Resets Status to "Pending" and
    /// re-runs the fraud engine (usp_Property_Update's own behaviour).
    /// Ownership is enforced by the stored procedure itself: if
    /// <paramref name="sellerId"/> does not own <paramref name="propertyId"/>,
    /// usp_Property_Update raises a SqlException, which
    /// ExceptionHandlingMiddleware turns into a 400 - this method never
    /// silently no-ops on a mismatched owner.
    /// </summary>
    Task<Result<PropertyListingResult>> UpdateAsync(
        int propertyId, UpdatePropertyRequest request, int sellerId, CancellationToken cancellationToken = default);

    /// <summary>Deletes a listing. Owner-or-Admin is enforced by usp_Property_Delete itself.</summary>
    Task<Result> DeleteAsync(int propertyId, int callerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Withdraws a listing the caller owns (Phase F - Property Withdrawal /
    /// Soft Delete). Sets Status to "Withdrawn" without deleting the row or
    /// any child/audit record - this is the Seller-facing replacement for
    /// "Delete" (see usp_Property_Withdraw's own header comment for why:
    /// DeedVerification history cannot be safely hard-deleted). Ownership,
    /// and which source states may be withdrawn (Pending/Approved only),
    /// are enforced by the stored procedure itself, exactly like
    /// <see cref="UpdateAsync"/> - a mismatched owner or a disallowed
    /// source state (Flagged/Rejected/already-Withdrawn) raises a
    /// SqlException rather than silently no-opping.
    /// </summary>
    Task<Result<PropertyListingResult>> WithdrawAsync(
        int propertyId, int sellerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes one image belonging to a listing the caller owns (or any
    /// listing, for an Admin - usp_PropertyImage_Delete has no ownership
    /// check of its own, exactly like usp_PropertyImage_Add, so "owner or
    /// Admin" is enforced entirely at this layer, the same split
    /// <see cref="AddImageAsync"/> already uses for this sub-resource).
    /// Deletes the dbo.PropertyImage row, then the physical file (the
    /// database row is the source of truth: if the physical delete fails
    /// or the file is already gone, that does not undo the successful
    /// database deletion - see LocalFileStorageService.DeleteImageAsync's
    /// doc comment). If the deleted image was Primary, the stored
    /// procedure promotes another remaining image automatically; if no
    /// images remain, the property is left with none, which every other
    /// read path (GetById, Search, GetBySeller) already tolerates. Re-runs
    /// the fraud engine afterwards for the same reason AddImageAsync does -
    /// Duplicate Image and Missing Information both depend on which images
    /// currently exist. Returns the refreshed <see cref="PropertyDetail"/>.
    /// </summary>
    Task<Result<PropertyDetail>> DeleteImageAsync(
        int propertyId,
        int imageId,
        int callerId,
        string? callerRole,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Contact Seller workflow: returns the Seller's name/phone/email/
    /// verified-badge for <paramref name="propertyId"/> - Buyer-only at the
    /// controller (RequireBuyer policy), and gated here, server-side, on
    /// the property currently being "Approved" (never trusts that the
    /// frontend already checked this). A Pending/Flagged/Rejected/
    /// Disapproved/Withdrawn property, or a nonexistent one, both return the
    /// same generic "Property not found" failure - the same account-
    /// enumeration-safe pattern <see cref="GetByIdAsync"/> already uses -
    /// so a Buyer cannot distinguish "doesn't exist" from "not public yet"
    /// by probing this endpoint. <paramref name="buyerId"/> is not currently
    /// used to further restrict the result (any authenticated Buyer may
    /// request contact details for any Approved listing - this is not a
    /// per-buyer allowlist), but is threaded through for symmetry with
    /// every other authenticated action on this interface and for future
    /// auditing.
    /// </summary>
    Task<Result<SellerContactInfo>> GetSellerContactAsync(
        int propertyId, int buyerId, CancellationToken cancellationToken = default);
}
