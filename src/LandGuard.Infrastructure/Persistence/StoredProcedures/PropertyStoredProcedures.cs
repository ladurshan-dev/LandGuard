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

        // BUG FIX (PropertyID = 0 on create): usp_Property_Create's own
        // final statement is "SELECT * FROM dbo.vw_PropertyListing WHERE
        // PropertyID = @NewPropertyID", but it gets there only after calling
        // EXEC dbo.usp_Fraud_AnalyseProperty, which itself calls EXEC
        // dbo.usp_Risk_GenerateReport - and that procedure also SELECTs (the
        // RiskReport row), as does usp_Fraud_AnalyseProperty's own trailing
        // "SELECT @FraudCheckID AS FraudCheckID". That makes this a
        // 3-result-set batch, in this exact order:
        //   1) RiskReport columns (ReportID, FraudCheckID, RiskScore, ...)
        //   2) FraudCheckID
        //   3) the actual property listing row (PropertyListingResult shape)
        // QuerySingleOrDefaultAsync<PropertyListingResult> (previously used
        // here) only ever reads the FIRST result set - it was silently
        // mapping the RiskReport row onto PropertyListingResult, so every
        // column without a same-named match (PropertyId included) was left
        // at its default, i.e. PropertyId = 0. QueryMultipleAsync, the same
        // approach GetByIdAsync already uses below for usp_Property_GetById,
        // reads each result set in its actual declared order instead. The
        // first two are read and discarded (they exist only as a side
        // effect of the inline fraud analysis; a real analysis result is
        // still returned separately by AnalyseAsync/IPropertyStoredProcedures
        // whenever a caller actually needs it), and the third and final one
        // is the property listing this method is meant to return - the real
        // SCOPE_IDENTITY()-derived PropertyID (already correct in the
        // stored procedure) finally reaches PropertyListingResult.PropertyId
        // intact.
        using var multi = await _executor.QueryMultipleAsync(
            "dbo.usp_Property_Create", parameters, cancellationToken);

        await multi.ReadSingleOrDefaultAsync<dynamic>();  // RiskReport row - discarded
        await multi.ReadSingleOrDefaultAsync<dynamic>();  // FraudCheckID - discarded
        var listing = await multi.ReadSingleOrDefaultAsync<PropertyListingResult>();

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
        //
        // BUG FIX (identical to CreateAsync's above): usp_Property_Update
        // also calls EXEC dbo.usp_Fraud_AnalyseProperty (which itself calls
        // EXEC dbo.usp_Risk_GenerateReport) before its own final
        // "SELECT * FROM dbo.vw_PropertyListing WHERE PropertyID = @PropertyID",
        // making this the same 3-result-set batch as usp_Property_Create:
        //   1) RiskReport columns (ReportID, FraudCheckID, RiskScore, ...)
        //   2) FraudCheckID
        //   3) the actual property listing row (PropertyListingResult shape)
        // QuerySingleOrDefaultAsync<PropertyListingResult> only reads the
        // first result set, so it was mapping the RiskReport row onto
        // PropertyListingResult - PropertyId (and every other column absent
        // from that first result set) stayed at its default. QueryMultipleAsync
        // reads each result set in its actual declared order instead: the
        // first two are read and discarded, and the third and final one is
        // the property listing this method is meant to return.
        using var multi = await _executor.QueryMultipleAsync(
            "dbo.usp_Property_Update", parameters, cancellationToken);

        await multi.ReadSingleOrDefaultAsync<dynamic>();  // RiskReport row - discarded
        await multi.ReadSingleOrDefaultAsync<dynamic>();  // FraudCheckID - discarded
        var listing = await multi.ReadSingleOrDefaultAsync<PropertyListingResult>();

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
}
