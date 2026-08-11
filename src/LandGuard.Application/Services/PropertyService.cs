using FluentValidation;
using LandGuard.Application.Common.Interfaces;
using LandGuard.Application.Common.Interfaces.StoredProcedures;
using LandGuard.Application.Common.Models;
using LandGuard.Application.DTOs.Property;
using LandGuard.Application.DTOs.Property.Validators;
using LandGuard.Domain.Enums;

namespace LandGuard.Application.Services;

/// <summary>
/// Orchestrates Property CRUD, image upload and the visibility/ownership
/// rules around them. Contains no SQL and no HTTP - it composes
/// <see cref="IPropertyStoredProcedures"/> (data access),
/// <see cref="IGeocodingService"/> and <see cref="IFileStorageService"/>
/// (both Infrastructure concerns reached only through their
/// Application-defined interfaces), exactly the shape
/// <see cref="AuthService"/> established in Module 3.
///
/// Ownership for Update/Delete is enforced by the stored procedures
/// themselves (see IPropertyStoredProcedures' doc comments) - this class
/// never re-implements that check for those two operations, only passes
/// the caller's own id through. AddImage and the visibility rule on
/// GetById/GetBySeller have no equivalent database-side check (the
/// procedures behind them are plain reads/inserts with no caller
/// awareness), so those are enforced here instead.
/// </summary>
public class PropertyService : IPropertyService
{
    private readonly IPropertyStoredProcedures _propertyStoredProcedures;
    private readonly IUserStoredProcedures _userStoredProcedures;
    private readonly IGeocodingService _geocodingService;
    private readonly IFileStorageService _fileStorageService;
    private readonly IValidator<CreatePropertyRequest> _createValidator;
    private readonly IValidator<UpdatePropertyRequest> _updateValidator;
    private readonly IValidator<PropertySearchRequest> _searchValidator;

    private static readonly string AdminRoleValue = UserRole.Administrator.ToDbValue();

    public PropertyService(
        IPropertyStoredProcedures propertyStoredProcedures,
        IUserStoredProcedures userStoredProcedures,
        IGeocodingService geocodingService,
        IFileStorageService fileStorageService,
        IValidator<CreatePropertyRequest> createValidator,
        IValidator<UpdatePropertyRequest> updateValidator,
        IValidator<PropertySearchRequest> searchValidator)
    {
        _propertyStoredProcedures = propertyStoredProcedures;
        _userStoredProcedures = userStoredProcedures;
        _geocodingService = geocodingService;
        _fileStorageService = fileStorageService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _searchValidator = searchValidator;
    }

    public async Task<Result<PropertyListingResult>> CreateAsync(
        CreatePropertyRequest request, int sellerId, CancellationToken cancellationToken = default)
    {
        // Seller Government Identity Verification requirement: server-side
        // enforcement, checked first (before validation, geocoding, or any
        // stored-procedure call) - an unverified Seller must never even
        // reach usp_Property_Create, which also re-checks this itself as
        // defence-in-depth (see that procedure's own header comment).
        var sellerProfile = await _userStoredProcedures.GetByIdAsync(sellerId, cancellationToken);
        if (sellerProfile is null || !string.Equals(sellerProfile.IdentityStatus, "Verified", StringComparison.Ordinal))
        {
            return Result<PropertyListingResult>.Failure("Your identity must be verified before you can list a property.");
        }

        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var (latitude, longitude) = await ResolveCoordinatesAsync(
            request.Latitude, request.Longitude, request.Location, request.District, cancellationToken);

        // usp_Property_Create already runs usp_Fraud_AnalyseProperty once
        // internally, so the returned row's RiskLevel/FraudStatus reflect a
        // zero-image analysis - expected to under-report risk until photos
        // are attached via AddImageAsync, which re-runs the engine.
        var listing = await _propertyStoredProcedures.CreateAsync(
            sellerId,
            request.Title,
            request.Description,
            request.Location,
            request.District,
            latitude,
            longitude,
            request.Size,
            request.Price,
            request.DeedReference,
            request.OwnerName,
            request.OwnerNic,
            request.OwnerAddress,
            cancellationToken);

        return Result<PropertyListingResult>.Success(listing);
    }

    public async Task<Result<PropertyDetail>> AddImageAsync(
        int propertyId,
        string fileName,
        string contentType,
        Stream content,
        bool isPrimary,
        int callerId,
        string? callerRole,
        CancellationToken cancellationToken = default)
    {
        var existing = await _propertyStoredProcedures.GetByIdAsync(propertyId, cancellationToken);
        if (existing is null)
        {
            return Result<PropertyDetail>.Failure("Property not found.");
        }

        if (existing.Listing.SellerId != callerId && !IsAdmin(callerRole))
        {
            return Result<PropertyDetail>.Failure("Only the property's owner or an administrator may add images to it.");
        }

        if (!PropertyValidationRules.AllowedImageContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            return Result<PropertyDetail>.Failure(
                $"Unsupported image type '{contentType}'. Allowed: {string.Join(", ", PropertyValidationRules.AllowedImageContentTypes)}.");
        }

        if (content.CanSeek && content.Length > PropertyValidationRules.MaxImageSizeBytes)
        {
            return Result<PropertyDetail>.Failure(
                $"Image exceeds the maximum allowed size of {PropertyValidationRules.MaxImageSizeBytes / (1024 * 1024)} MB.");
        }

        var stored = await _fileStorageService.SaveImageAsync(propertyId, fileName, contentType, content, cancellationToken);

        await _propertyStoredProcedures.AddImageAsync(
            propertyId, stored.Url, stored.Sha256Hash, isPrimary, cancellationToken);

        // Re-run the engine now that this image exists, so Duplicate Image
        // and Missing Information reflect the listing's true current state
        // (see IPropertyStoredProcedures.AnalyseAsync's doc comment).
        await _propertyStoredProcedures.AnalyseAsync(propertyId, cancellationToken);

        var refreshed = await _propertyStoredProcedures.GetByIdAsync(propertyId, cancellationToken);

        return Result<PropertyDetail>.Success(refreshed!);
    }

    public async Task<Result<PropertyDetail>> GetByIdAsync(
        int propertyId, int? callerId, string? callerRole, CancellationToken cancellationToken = default)
    {
        var detail = await _propertyStoredProcedures.GetByIdAsync(propertyId, cancellationToken);
        if (detail is null)
        {
            return Result<PropertyDetail>.Failure("Property not found.");
        }

        var isOwner = callerId.HasValue && detail.Listing.SellerId == callerId.Value;
        var isAdmin = IsAdmin(callerRole);
        var isPublished = string.Equals(detail.Listing.Status, "Approved", StringComparison.Ordinal);

        if (!isPublished && !isOwner && !isAdmin)
        {
            // Same shape as a nonexistent id - never confirm to an
            // unrelated caller that a Pending/Flagged/Rejected listing
            // exists (the same account-enumeration-safe pattern Module 3's
            // login endpoint uses).
            return Result<PropertyDetail>.Failure("Property not found.");
        }

        // Buyer privacy requirement: a Buyer/anonymous caller reaching this
        // point only ever does so because the listing is Approved and they
        // are not its owner (isOwner/isAdmin both false) - Approval is
        // sufficient information for them. Internal fraud-engine output
        // (the rule-by-rule report, and the score/level/status/summary on
        // the listing itself) must never reach them, even though the
        // listing itself is visible. The owning Seller and an Admin still
        // see everything, completely unchanged.
        if (!isOwner && !isAdmin)
        {
            RedactFraudFields(detail.Listing);
            RedactOwnerFields(detail.Listing);
            RedactSellerContactFields(detail.Listing);
            detail.FraudReport = Array.Empty<PropertyFraudRuleResult>();
        }

        return Result<PropertyDetail>.Success(detail);
    }

    /// <summary>
    /// Contact Seller workflow: see <see cref="IPropertyService.GetSellerContactAsync"/>'s
    /// own doc comment for the full reasoning. Reads the Seller's profile
    /// via <see cref="IUserStoredProcedures"/> (the same source
    /// <see cref="CreateAsync"/> already reads Name/Email/Phone/NicVerified
    /// from) rather than adding a new SQL projection just for this - Email
    /// in particular is not part of dbo.vw_PropertyListing at all (only
    /// Name/Phone are - see 03_Views.sql), so PropertyListingResult/
    /// PropertySearchResult could never have carried it even before the
    /// redaction fix, and there is no reason to add it there now that a
    /// dedicated, gated endpoint exists.
    /// </summary>
    public async Task<Result<SellerContactInfo>> GetSellerContactAsync(
        int propertyId, int buyerId, CancellationToken cancellationToken = default)
    {
        var detail = await _propertyStoredProcedures.GetByIdAsync(propertyId, cancellationToken);

        if (detail is null || !string.Equals(detail.Listing.Status, "Approved", StringComparison.Ordinal))
        {
            return Result<SellerContactInfo>.Failure("Property not found.");
        }

        var seller = await _userStoredProcedures.GetByIdAsync(detail.Listing.SellerId, cancellationToken);
        if (seller is null)
        {
            return Result<SellerContactInfo>.Failure("Property not found.");
        }

        return Result<SellerContactInfo>.Success(new SellerContactInfo
        {
            SellerName = seller.Name,
            Phone = seller.Phone,
            Email = seller.Email,
            VerifiedSeller = seller.NicVerified
        });
    }

    public async Task<Result<PropertySearchResponse>> SearchAsync(
        PropertySearchRequest request, string? callerRole, CancellationToken cancellationToken = default)
    {
        await _searchValidator.ValidateAndThrowAsync(request, cancellationToken);

        var isAdmin = IsAdmin(callerRole);

        // Buyer privacy requirement: this endpoint is reachable anonymously
        // and only ever returns Approved listings (usp_Property_Search
        // reads dbo.vw_PublishedProperty), but a non-Admin caller must not
        // be able to filter or sort by the internal risk band either - that
        // would let a Buyer indirectly reconstruct which Approved listings
        // the fraud engine flagged as higher risk, without ever seeing a
        // raw score. Forced off here, server-side, rather than trusted from
        // the request - an Admin caller keeps full capability unchanged.
        var riskLevel = isAdmin ? request.RiskLevel : null;
        var sortBy = !isAdmin && string.Equals(request.SortBy, "RiskAsc", StringComparison.Ordinal)
            ? "Newest"
            : request.SortBy;

        var rows = await _propertyStoredProcedures.SearchAsync(
            request.Keyword,
            request.District,
            request.MinPrice,
            request.MaxPrice,
            request.MinSize,
            request.MaxSize,
            riskLevel,
            sortBy,
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        if (!isAdmin)
        {
            foreach (var row in rows)
            {
                RedactFraudFields(row);
                RedactOwnerFields(row);
                RedactSellerContactFields(row);
            }
        }

        var response = new PropertySearchResponse
        {
            Items = rows,
            TotalRecords = rows.Count > 0 ? rows[0].TotalRecords : 0,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };

        return Result<PropertySearchResponse>.Success(response);
    }

    public async Task<Result<IReadOnlyList<PropertyListingResult>>> GetBySellerAsync(
        int sellerId, int callerId, string? callerRole, CancellationToken cancellationToken = default)
    {
        if (sellerId != callerId && !IsAdmin(callerRole))
        {
            return Result<IReadOnlyList<PropertyListingResult>>.Failure("You may only view your own listings.");
        }

        var listings = await _propertyStoredProcedures.GetBySellerAsync(sellerId, cancellationToken);

        return Result<IReadOnlyList<PropertyListingResult>>.Success(listings);
    }

    public async Task<Result<PropertyListingResult>> UpdateAsync(
        int propertyId, UpdatePropertyRequest request, int sellerId, CancellationToken cancellationToken = default)
    {
        await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        var latitude = request.Latitude;
        var longitude = request.Longitude;

        if (!latitude.HasValue && !longitude.HasValue && request.RegeocodeLocation)
        {
            var location = request.Location;
            var district = request.District;

            // Only one of Location/District may have changed - fall back to
            // the listing's current value for whichever wasn't supplied, so
            // the geocode query is always complete.
            if (location is null || district is null)
            {
                var current = await _propertyStoredProcedures.GetByIdAsync(propertyId, cancellationToken);
                location ??= current?.Listing.Location;
                district ??= current?.Listing.District;
            }

            if (location is not null)
            {
                var geocoded = await _geocodingService.GeocodeAsync(BuildGeocodeQuery(location, district), cancellationToken);
                if (geocoded is not null)
                {
                    latitude = geocoded.Latitude;
                    longitude = geocoded.Longitude;
                }
            }
        }

        // Ownership is enforced by usp_Property_Update itself - a mismatched
        // sellerId raises a SqlException rather than returning here.
        var updated = await _propertyStoredProcedures.UpdateAsync(
            propertyId,
            sellerId,
            request.Title,
            request.Description,
            request.Location,
            request.District,
            latitude,
            longitude,
            request.Size,
            request.Price,
            request.DeedReference,
            request.OwnerName,
            request.OwnerNic,
            request.OwnerAddress,
            cancellationToken);

        return Result<PropertyListingResult>.Success(updated);
    }

    public async Task<Result> DeleteAsync(int propertyId, int callerId, CancellationToken cancellationToken = default)
    {
        // Owner-or-Admin is enforced by usp_Property_Delete itself (raises
        // a SqlException, translated to 400, if neither). A non-existent
        // PropertyID deletes zero rows without raising - handled below.
        var rowsDeleted = await _propertyStoredProcedures.DeleteAsync(propertyId, callerId, cancellationToken);

        return rowsDeleted > 0
            ? Result.Success()
            : Result.Failure("Property not found.");
    }

    public async Task<Result<PropertyListingResult>> WithdrawAsync(
        int propertyId, int sellerId, CancellationToken cancellationToken = default)
    {
        // Ownership and the allowed source-state transitions (Pending/Approved
        // only) are enforced by usp_Property_Withdraw itself - a mismatched
        // sellerId or a disallowed source state (Flagged/Rejected/already
        // Withdrawn) raises a SqlException rather than returning here,
        // exactly like UpdateAsync above.
        var withdrawn = await _propertyStoredProcedures.WithdrawAsync(propertyId, sellerId, cancellationToken);

        return Result<PropertyListingResult>.Success(withdrawn);
    }

    public async Task<Result<PropertyDetail>> DeleteImageAsync(
        int propertyId,
        int imageId,
        int callerId,
        string? callerRole,
        CancellationToken cancellationToken = default)
    {
        var existing = await _propertyStoredProcedures.GetByIdAsync(propertyId, cancellationToken);
        if (existing is null)
        {
            return Result<PropertyDetail>.Failure("Property not found.");
        }

        if (existing.Listing.SellerId != callerId && !IsAdmin(callerRole))
        {
            return Result<PropertyDetail>.Failure("Only the property's owner or an administrator may delete images from it.");
        }

        var image = existing.Images.FirstOrDefault(i => i.ImageId == imageId);
        if (image is null)
        {
            return Result<PropertyDetail>.Failure("Image not found.");
        }

        // Delete the database row (and let the stored procedure promote a
        // new Primary image if the deleted one was Primary) before
        // touching the filesystem - the database row is the source of
        // truth for whether the image exists, not the file.
        await _propertyStoredProcedures.DeleteImageAsync(propertyId, imageId, cancellationToken);

        // Best-effort physical cleanup: a storage failure here must not
        // undo or fail the already-successful database deletion above (see
        // IFileStorageService.DeleteImageAsync's doc comment).
        try
        {
            await _fileStorageService.DeleteImageAsync(image.ImageUrl, cancellationToken);
        }
        catch (IOException)
        {
            // Physical file locked/already gone - the database record is
            // already gone, which is what every other read path relies on.
        }
        catch (UnauthorizedAccessException)
        {
            // Filesystem permission issue - same reasoning as above.
        }

        // Re-run the engine now that this image is gone, so Duplicate
        // Image and Missing Information reflect the listing's true current
        // state (see IPropertyStoredProcedures.AnalyseAsync's doc comment
        // and AddImageAsync's identical call above).
        await _propertyStoredProcedures.AnalyseAsync(propertyId, cancellationToken);

        var refreshed = await _propertyStoredProcedures.GetByIdAsync(propertyId, cancellationToken);

        return Result<PropertyDetail>.Success(refreshed!);
    }

    private async Task<(decimal? Latitude, decimal? Longitude)> ResolveCoordinatesAsync(
        decimal? latitude, decimal? longitude, string location, string? district, CancellationToken cancellationToken)
    {
        if (latitude.HasValue || longitude.HasValue)
        {
            return (latitude, longitude);
        }

        var geocoded = await _geocodingService.GeocodeAsync(BuildGeocodeQuery(location, district), cancellationToken);

        return geocoded is null ? (null, null) : (geocoded.Latitude, geocoded.Longitude);
    }

    private static string BuildGeocodeQuery(string location, string? district) =>
        string.IsNullOrWhiteSpace(district) ? $"{location}, Sri Lanka" : $"{location}, {district}, Sri Lanka";

    private static bool IsAdmin(string? callerRole) => string.Equals(callerRole, AdminRoleValue, StringComparison.Ordinal);

    /// <summary>
    /// Buyer privacy requirement: strips every internal fraud-engine field
    /// from one listing row in place - RiskScore/RiskLevel/FraudStatus/
    /// RiskSummary/RiskGeneratedDate all become null. Called from
    /// GetByIdAsync (one row) and SearchAsync (every row) for any caller
    /// who is neither the listing's owner nor an Admin. Two overloads
    /// rather than a shared base type because PropertyListingResult and
    /// PropertySearchResult are deliberately separate Dapper projection
    /// DTOs (see PropertySearchResult's own doc comment) - not worth a
    /// shared interface just for this.
    /// </summary>
    private static void RedactFraudFields(PropertyListingResult listing)
    {
        listing.RiskScore = null;
        listing.RiskLevel = null;
        listing.FraudStatus = null;
        listing.RiskSummary = null;
        listing.RiskGeneratedDate = null;
    }

    /// <summary>
    /// Owner Name / Owner NIC / Owner Address requirement, plus the Deed-
    /// Reference privacy fix (manual-testing finding): this explicit
    /// deed-owner data, and the deed reference itself, exist purely to
    /// support the Government Deed Verification pipeline - none of it was
    /// ever meant to be marketplace-facing, and OwnerNic/DeedReference in
    /// particular are sensitive (OwnerNic is PII; DeedReference is a real
    /// legal-document identifier that has no business being visible to a
    /// Buyer browsing a listing). ROOT CAUSE of the leak this fixes:
    /// DeedReference was not included here even though the three OwnerX
    /// fields already were - Buyers received it unredacted on both
    /// GET /api/properties/{id} and GET /api/properties (search) until now.
    /// Redacted alongside RedactFraudFields/RedactSellerContactFields for
    /// exactly the same callers (non-owner, non-Admin) and via the same
    /// two-overload shape - see RedactFraudFields' own doc comment.
    /// </summary>
    private static void RedactOwnerFields(PropertyListingResult listing)
    {
        listing.OwnerName = null;
        listing.OwnerNic = null;
        listing.OwnerAddress = null;
        listing.DeedReference = null;
    }

    /// <summary>See the <see cref="PropertyListingResult"/> overload's doc comment.</summary>
    private static void RedactOwnerFields(PropertySearchResult listing)
    {
        listing.OwnerName = null;
        listing.OwnerNic = null;
        listing.OwnerAddress = null;
        listing.DeedReference = null;
    }

    /// <summary>See the <see cref="PropertyListingResult"/> overload's doc comment.</summary>
    private static void RedactFraudFields(PropertySearchResult listing)
    {
        listing.RiskScore = null;
        listing.RiskLevel = null;
        listing.FraudStatus = null;
        listing.RiskSummary = null;
        listing.RiskGeneratedDate = null;
    }

    /// <summary>
    /// Contact Seller workflow (manual-testing finding): SellerPhone was
    /// previously returned unredacted to every caller, including a Buyer
    /// who had never asked to contact the Seller - it is no longer part of
    /// the general property read for a non-owner/non-Admin caller at all.
    /// The only way to obtain it now is the dedicated, Approved-gated
    /// <see cref="GetSellerContactAsync"/> endpoint. SellerName and
    /// SellerNicVerified deliberately remain visible here - the Buyer
    /// contact-workflow requirement explicitly allows the Seller's display
    /// name and a "Verified Seller" badge to show before contact is
    /// requested, only phone/email/NIC are gated.
    /// </summary>
    private static void RedactSellerContactFields(PropertyListingResult listing)
    {
        listing.SellerPhone = null;
    }

    /// <summary>See the <see cref="PropertyListingResult"/> overload's doc comment.</summary>
    private static void RedactSellerContactFields(PropertySearchResult listing)
    {
        listing.SellerPhone = null;
    }
}
