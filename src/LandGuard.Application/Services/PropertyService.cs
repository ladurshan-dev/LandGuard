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
    private readonly IGeocodingService _geocodingService;
    private readonly IFileStorageService _fileStorageService;
    private readonly IValidator<CreatePropertyRequest> _createValidator;
    private readonly IValidator<UpdatePropertyRequest> _updateValidator;
    private readonly IValidator<PropertySearchRequest> _searchValidator;

    private static readonly string AdminRoleValue = UserRole.Administrator.ToDbValue();

    public PropertyService(
        IPropertyStoredProcedures propertyStoredProcedures,
        IGeocodingService geocodingService,
        IFileStorageService fileStorageService,
        IValidator<CreatePropertyRequest> createValidator,
        IValidator<UpdatePropertyRequest> updateValidator,
        IValidator<PropertySearchRequest> searchValidator)
    {
        _propertyStoredProcedures = propertyStoredProcedures;
        _geocodingService = geocodingService;
        _fileStorageService = fileStorageService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _searchValidator = searchValidator;
    }

    public async Task<Result<PropertyListingResult>> CreateAsync(
        CreatePropertyRequest request, int sellerId, CancellationToken cancellationToken = default)
    {
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
        var isPublished = string.Equals(detail.Listing.Status, "Approved", StringComparison.Ordinal);

        if (!isPublished && !isOwner && !IsAdmin(callerRole))
        {
            // Same shape as a nonexistent id - never confirm to an
            // unrelated caller that a Pending/Flagged/Rejected listing
            // exists (the same account-enumeration-safe pattern Module 3's
            // login endpoint uses).
            return Result<PropertyDetail>.Failure("Property not found.");
        }

        return Result<PropertyDetail>.Success(detail);
    }

    public async Task<Result<PropertySearchResponse>> SearchAsync(
        PropertySearchRequest request, CancellationToken cancellationToken = default)
    {
        await _searchValidator.ValidateAndThrowAsync(request, cancellationToken);

        var rows = await _propertyStoredProcedures.SearchAsync(
            request.Keyword,
            request.District,
            request.MinPrice,
            request.MaxPrice,
            request.MinSize,
            request.MaxSize,
            request.RiskLevel,
            request.SortBy,
            request.PageNumber,
            request.PageSize,
            cancellationToken);

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
}
