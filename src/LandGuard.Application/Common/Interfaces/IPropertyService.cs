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

    /// <summary>Public search over published (Approved, active-seller) listings only - FR10.</summary>
    Task<Result<PropertySearchResponse>> SearchAsync(
        PropertySearchRequest request, CancellationToken cancellationToken = default);

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
}
