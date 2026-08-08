using LandGuard.Application.Common.Models;

namespace LandGuard.Application.Common.Interfaces.StoredProcedures;

/// <summary>
/// Application-layer contract over LandGuardDB's Property CRUD and
/// fraud-trigger stored procedures. Implemented in Infrastructure using
/// Dapper (see <c>PropertyStoredProcedures</c>), following exactly the
/// shape <c>INotificationStoredProcedures</c> and
/// <c>IUserStoredProcedures</c> established - Application only ever sees
/// this interface and plain DTOs, never a SQL string or a Dapper type.
///
/// <c>usp_Property_Create</c> and <c>usp_Property_Update</c> already
/// trigger <c>usp_Fraud_AnalyseProperty</c> internally, but the engine
/// only sees whatever images exist at that exact moment - images are
/// attached afterwards via <see cref="AddImageAsync"/>, so
/// <see cref="AnalyseAsync"/> is exposed separately for
/// <c>PropertyService</c> to call again once the seller's photos are in,
/// exactly as <c>Database/Docs/API_Mapping.md</c>'s submission sequence
/// documents (Create -&gt; AddImage(s) -&gt; Analyse).
/// </summary>
public interface IPropertyStoredProcedures
{
    /// <summary>Wraps usp_Property_Create. Status is always "Pending" on the returned row - the engine then re-scores it, but scoring against zero images is expected to under-report risk until AddImageAsync + AnalyseAsync run.</summary>
    Task<PropertyListingResult> CreateAsync(
        int sellerId,
        string title,
        string? description,
        string location,
        string? district,
        decimal? latitude,
        decimal? longitude,
        double size,
        decimal price,
        string? deedReference,
        CancellationToken cancellationToken = default);

    /// <summary>Wraps usp_PropertyImage_Add. Returns the new ImageID.</summary>
    Task<int> AddImageAsync(
        int propertyId, string imageUrl, string? imageHash, bool isPrimary, CancellationToken cancellationToken = default);

    /// <summary>Wraps usp_Fraud_AnalyseProperty. Returns the new FraudCheckID (the analysis run this call just recorded).</summary>
    Task<int> AnalyseAsync(int propertyId, CancellationToken cancellationToken = default);

    /// <summary>Wraps usp_Property_GetById's 3 result sets (listing, images, fraud report) into one composite. Null if the property doesn't exist.</summary>
    Task<PropertyDetail?> GetByIdAsync(int propertyId, CancellationToken cancellationToken = default);

    /// <summary>Wraps usp_Property_Search. Every returned row carries the same TotalRecords for the pager.</summary>
    Task<IReadOnlyList<PropertySearchResult>> SearchAsync(
        string? keyword,
        string? district,
        decimal? minPrice,
        decimal? maxPrice,
        double? minSize,
        double? maxSize,
        string? riskLevel,
        string sortBy,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>Wraps usp_Property_GetBySeller - every status, not just Approved (the seller's own dashboard grid, FR08).</summary>
    Task<IReadOnlyList<PropertyListingResult>> GetBySellerAsync(int sellerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Wraps usp_Property_Update. <paramref name="sellerId"/> must be the
    /// caller's own id (never trust a client-supplied owner id) - the
    /// procedure itself re-checks that this seller owns the property and
    /// raises a SqlException (translated to 400 by
    /// ExceptionHandlingMiddleware) if not, so this is the one place
    /// ownership is actually enforced. Null return means "not found or not
    /// yours" was raised as an exception, not a silent null - the
    /// signature is non-nullable on success.
    /// </summary>
    Task<PropertyListingResult> UpdateAsync(
        int propertyId,
        int sellerId,
        string? title,
        string? description,
        string? location,
        string? district,
        decimal? latitude,
        decimal? longitude,
        double? size,
        decimal? price,
        string? deedReference,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Wraps usp_Property_Delete. <paramref name="callerUserId"/> must be
    /// the caller's own id - the procedure is the actual authorization
    /// boundary here (owner or an active Admin), raising a SqlException
    /// otherwise. Returns rows deleted (0 or 1).
    /// </summary>
    Task<int> DeleteAsync(int propertyId, int callerUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Wraps usp_PropertyImage_Delete. No ownership check of its own (same
    /// as <see cref="AddImageAsync"/>/usp_PropertyImage_Add) -
    /// PropertyService.DeleteImageAsync already resolves and authorizes
    /// the image before calling this. Applies the "never leave a property
    /// with zero primary images while images remain" rule server-side: if
    /// the deleted image was primary, the oldest remaining image (lowest
    /// ImageID) for the same property is promoted to primary in the same
    /// call.
    /// </summary>
    Task DeleteImageAsync(int propertyId, int imageId, CancellationToken cancellationToken = default);
}
