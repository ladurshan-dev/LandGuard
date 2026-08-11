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
        string deedReference,
        string ownerName,
        string ownerNic,
        string ownerAddress,
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
        string? ownerName,
        string? ownerNic,
        string? ownerAddress,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Wraps usp_Property_Delete. <paramref name="callerUserId"/> must be
    /// the caller's own id - the procedure is the actual authorization
    /// boundary here (owner or an active Admin), raising a SqlException
    /// otherwise. Returns rows deleted (0 or 1).
    /// </summary>
    Task<int> DeleteAsync(int propertyId, int callerUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Wraps usp_Property_Withdraw (Phase F - Property Withdrawal / Soft
    /// Delete). Seller-only (never Admin - Admin's cleanup path stays
    /// <see cref="DeleteAsync"/>). <paramref name="sellerId"/> must be the
    /// caller's own id, the same rule <see cref="UpdateAsync"/> follows -
    /// ownership is enforced by the procedure itself. Sets Status to
    /// "Withdrawn" without touching any child/audit record; a Flagged,
    /// Rejected, or already-Withdrawn property raises a SqlException
    /// (translated to 400) with a specific reason instead of silently
    /// doing nothing.
    /// </summary>
    Task<PropertyListingResult> WithdrawAsync(int propertyId, int sellerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Wraps usp_PropertyImage_Delete. Deliberately no ownership check
    /// here, mirroring <see cref="AddImageAsync"/> rather than
    /// <see cref="UpdateAsync"/>/<see cref="WithdrawAsync"/> - the
    /// procedure itself has none either (see its own header comment),
    /// because <c>PropertyService.DeleteImageAsync</c> already resolves
    /// the property + image and enforces "owner or Admin" in C# before
    /// this is ever called, the same split <c>AddImageAsync</c> uses for
    /// this exact sub-resource. If <paramref name="imageId"/> doesn't
    /// belong to <paramref name="propertyId"/>, the procedure RAISERRORs
    /// ("Image not found") rather than silently affecting zero rows. The
    /// procedure also reassigns Primary to the oldest remaining image
    /// when the deleted one was Primary - nothing about that needs to
    /// surface here, the caller just re-reads the property afterwards.
    /// </summary>
    Task DeleteImageAsync(int propertyId, int imageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Wraps usp_Property_ApplyDeedVerificationOutcome (Mandatory Deed /
    /// Form-vs-Deed Verification requirement) - the SYSTEM-AUTOMATED
    /// counterpart to <see cref="IAdminStoredProcedures.ApprovePropertyAsync"/>/
    /// <c>RejectPropertyAsync</c>. Called only by
    /// <c>GovernmentDeedVerificationService.VerifyAndPersistAsync</c>, and
    /// only for a <paramref name="verificationStatus"/> of "Verified",
    /// "FormMismatch", "Fraudulent" or "PriceAnomaly" - never for
    /// "Unverified"/"UnverifiedCancelled" (a technical/OCR failure must not
    /// change Property.Status at all - see that service's own doc
    /// comment). A Withdrawn property raises a SqlException (translated to
    /// 400), the same guard <see cref="UpdateAsync"/>/
    /// <see cref="WithdrawAsync"/> already apply.
    /// </summary>
    /// <param name="verificationStatus">DeedVerificationStatus's exact string name - "Verified" | "FormMismatch" | "Fraudulent" | "PriceAnomaly" | "DuplicateProperty".</param>
    /// <param name="summary">The already-composed, Seller-facing explanation (GovernmentDeedFraudDetectionResult.Summary) - folded into the resulting Notification.Message, never re-derived here.</param>
    /// <param name="governmentPropertyReference">
    /// Global Duplicate-Property Prevention requirement: the resolved
    /// GovernmentLandRecordDto.PropertyReference for this run, if any -
    /// persisted onto Property.GovernmentPropertyReference in the same
    /// database write as the status change. Null when no government
    /// record was resolved at all (FormMismatch/Unverified/UnverifiedCancelled),
    /// in which case the column is left exactly as it already was
    /// (ISNULL-coalesce inside usp_Property_ApplyDeedVerificationOutcome).
    /// </param>
    /// <returns>
    /// The refreshed listing, plus EffectiveVerificationStatus - AUDIT-CONSISTENCY
    /// FIX (post-review, third pass): usp_Property_ApplyDeedVerificationOutcome's
    /// own concurrency-safe GovernmentPropertyReference check (see that
    /// procedure's "CONCURRENCY FIX" comment) can silently downgrade a
    /// "Verified"/"PriceAnomaly" <paramref name="verificationStatus"/> to
    /// "DuplicateProperty" if this call loses a race for the same
    /// reference. EffectiveVerificationStatus is what ACTUALLY happened -
    /// equal to <paramref name="verificationStatus"/> in every case except
    /// that downgrade. GovernmentDeedVerificationService.VerifyAndPersistAsync
    /// calls this method BEFORE persisting the DeedVerification audit row
    /// specifically so it can use this value to correct the audit record
    /// before writing it, instead of writing it first and risking
    /// disagreement with the final Property.Status - see that method's own
    /// "AUDIT-CONSISTENCY FIX" comment.
    /// </returns>
    Task<(PropertyListingResult Listing, string EffectiveVerificationStatus)> ApplyDeedVerificationOutcomeAsync(
        int propertyId, string verificationStatus, string? summary, string? governmentPropertyReference = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Wraps usp_Property_FindByGovernmentPropertyReference (Global
    /// Duplicate-Property Prevention requirement). Returns the PropertyID
    /// of a DIFFERENT property already carrying this same
    /// GovernmentPropertyReference, or null if none - deliberately returns
    /// only an id, never a Seller name/NIC/email or any other private
    /// data, so a caller cannot leak another Seller's information even by
    /// accident.
    /// </summary>
    Task<int?> FindPropertyIdByGovernmentPropertyReferenceAsync(
        string governmentPropertyReference, int excludePropertyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Wraps usp_Property_MarkPendingForReverification (status-safety
    /// correction to the Mandatory Deed / Form-vs-Deed Verification
    /// requirement). Called by
    /// <c>GovernmentDeedComparisonService.CompareAsync</c> the moment a
    /// re-verification begins on an already-owned property, immediately
    /// after that method's own ownership check and before any OCR/I-O that
    /// could fail - so a currently-Approved property is pulled out of
    /// Buyer visibility (vw_PublishedProperty only ever returns
    /// Status = 'Approved') for the duration of the attempt, rather than
    /// staying stale-Approved if the attempt then fails technically. A
    /// no-op for every status except Approved - see the underlying
    /// procedure's own header comment.
    /// </summary>
    Task MarkPendingForReverificationAsync(int propertyId, CancellationToken cancellationToken = default);
}
