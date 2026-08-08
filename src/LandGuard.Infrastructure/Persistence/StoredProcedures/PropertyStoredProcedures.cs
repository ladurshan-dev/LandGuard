using System.Data;
using Dapper;
using LandGuard.Application.Common.Interfaces.StoredProcedures;
using LandGuard.Application.Common.Models;

namespace LandGuard.Infrastructure.Persistence.StoredProcedures;

/// <summary>
/// Infrastructure implementation of <see cref="IPropertyStoredProcedures"/>,
/// following the pattern <c>NotificationStoredProcedures</c> and
/// <c>UserStoredProcedures</c> established: inject
/// <see cref="IStoredProcedureExecutor"/>, pass exact stored-procedure
/// parameter names as an anonymous object (or <see cref="DynamicParameters"/>
/// when an OUTPUT parameter is involved), map straight into plain DTOs from
/// <c>LandGuard.Application.Common.Models</c>. No business logic lives here -
/// ownership checks, fraud analysis, status transitions and notifications
/// are already handled inside the stored procedures themselves.
/// </summary>
public class PropertyStoredProcedures : IPropertyStoredProcedures
{
    private readonly IStoredProcedureExecutor _executor;

    public PropertyStoredProcedures(IStoredProcedureExecutor executor)
    {
        _executor = executor;
    }

    public async Task<PropertyListingResult> CreateAsync(
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
        CancellationToken cancellationToken = default)
    {
        // usp_Property_Create has an OUTPUT parameter (@NewPropertyID), the
        // same reason usp_User_Register needed DynamicParameters in Module 3.
        var parameters = new DynamicParameters();
        parameters.Add("@SellerID", sellerId);
        parameters.Add("@Title", title);
        parameters.Add("@Description", description);
        parameters.Add("@Location", location);
        parameters.Add("@District", district);
        parameters.Add("@Latitude", latitude);
        parameters.Add("@Longitude", longitude);
        parameters.Add("@Size", size);
        parameters.Add("@Price", price);
        parameters.Add("@DeedReference", deedReference);
        parameters.Add("@NewPropertyID", dbType: DbType.Int32, direction: ParameterDirection.Output);

        var listing = await _executor.QuerySingleOrDefaultAsync<PropertyListingResult>(
            "dbo.usp_Property_Create", parameters, cancellationToken);

        return listing!;
    }

    public async Task<int> AddImageAsync(
        int propertyId, string imageUrl, string? imageHash, bool isPrimary, CancellationToken cancellationToken = default)
    {
        var parameters = new { PropertyID = propertyId, ImageURL = imageUrl, ImageHash = imageHash, IsPrimary = isPrimary };

        // usp_PropertyImage_Add returns a single row with one column, ImageID.
        var imageId = await _executor.QuerySingleOrDefaultAsync<int>(
            "dbo.usp_PropertyImage_Add", parameters, cancellationToken);

        return imageId;
    }

    public async Task<int> AnalyseAsync(int propertyId, CancellationToken cancellationToken = default)
    {
        var parameters = new { PropertyID = propertyId };

        // usp_Fraud_AnalyseProperty returns a single row with one column, FraudCheckID.
        var fraudCheckId = await _executor.QuerySingleOrDefaultAsync<int>(
            "dbo.usp_Fraud_AnalyseProperty", parameters, cancellationToken);

        return fraudCheckId;
    }

    public async Task<PropertyDetail?> GetByIdAsync(int propertyId, CancellationToken cancellationToken = default)
    {
        var parameters = new { PropertyID = propertyId };

        // usp_Property_GetById returns 3 result sets in order: the listing
        // row, its images, and the rule-by-rule fraud report. Reading each
        // result set off the same GridReader in that exact order is the
        // one place in this class Dapper's multi-result-set support is
        // actually needed - see IStoredProcedureExecutor.QueryMultipleAsync.
        using var multi = await _executor.QueryMultipleAsync("dbo.usp_Property_GetById", parameters, cancellationToken);

        var listing = await multi.ReadSingleOrDefaultAsync<PropertyListingResult>();
        if (listing is null)
        {
            return null;
        }

        var images = (await multi.ReadAsync<PropertyImageSummary>()).AsList();
        var fraudReport = (await multi.ReadAsync<PropertyFraudRuleResult>()).AsList();

        return new PropertyDetail
        {
            Listing = listing,
            Images = images,
            FraudReport = fraudReport
        };
    }

    public Task<IReadOnlyList<PropertySearchResult>> SearchAsync(
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
        CancellationToken cancellationToken = default)
    {
        var parameters = new
        {
            Keyword = keyword,
            District = district,
            MinPrice = minPrice,
            MaxPrice = maxPrice,
            MinSize = minSize,
            MaxSize = maxSize,
            RiskLevel = riskLevel,
            SortBy = sortBy,
            PageNumber = pageNumber,
            PageSize = pageSize
        };

        return _executor.QueryAsync<PropertySearchResult>("dbo.usp_Property_Search", parameters, cancellationToken);
    }

    public Task<IReadOnlyList<PropertyListingResult>> GetBySellerAsync(int sellerId, CancellationToken cancellationToken = default)
    {
        var parameters = new { SellerID = sellerId };

        return _executor.QueryAsync<PropertyListingResult>("dbo.usp_Property_GetBySeller", parameters, cancellationToken);
    }

    public async Task<PropertyListingResult> UpdateAsync(
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
        CancellationToken cancellationToken = default)
    {
        var parameters = new
        {
            PropertyID = propertyId,
            SellerID = sellerId,
            Title = title,
            Description = description,
            Location = location,
            District = district,
            Latitude = latitude,
            Longitude = longitude,
            Size = size,
            Price = price,
            DeedReference = deedReference
        };

        // If sellerId doesn't own propertyId, the procedure RAISERRORs
        // before reaching its final SELECT - Dapper surfaces that as a
        // SqlException here, which this method deliberately does not
        // catch (see IPropertyStoredProcedures.UpdateAsync's doc comment).
        var listing = await _executor.QuerySingleOrDefaultAsync<PropertyListingResult>(
            "dbo.usp_Property_Update", parameters, cancellationToken);

        return listing!;
    }

    public async Task<int> DeleteAsync(int propertyId, int callerUserId, CancellationToken cancellationToken = default)
    {
        var parameters = new { PropertyID = propertyId, UserID = callerUserId };

        // usp_Property_Delete returns a single row with one column,
        // RowsDeleted - RAISERRORs first (SqlException) if the caller is
        // neither the owner nor an active Admin.
        var rowsDeleted = await _executor.QuerySingleOrDefaultAsync<int>(
            "dbo.usp_Property_Delete", parameters, cancellationToken);

        return rowsDeleted;
    }

    public Task DeleteImageAsync(int propertyId, int imageId, CancellationToken cancellationToken = default)
    {
        var parameters = new { PropertyID = propertyId, ImageID = imageId };

        // usp_PropertyImage_Delete has no result set to read (unlike
        // usp_PropertyImage_Add's single-column ImageID SELECT) - it's an
        // UPDATE/DELETE-only procedure, so ExecuteAsync is the right
        // executor method, matching IStoredProcedureExecutor's own doc
        // comment for that case.
        return _executor.ExecuteAsync("dbo.usp_PropertyImage_Delete", parameters, cancellationToken);
    }
}
