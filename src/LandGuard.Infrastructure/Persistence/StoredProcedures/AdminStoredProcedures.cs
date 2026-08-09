using LandGuard.Application.Common.Interfaces.StoredProcedures;
using LandGuard.Application.Common.Models;

namespace LandGuard.Infrastructure.Persistence.StoredProcedures;

/// <summary>
/// Infrastructure implementation of <see cref="IAdminStoredProcedures"/> -
/// Phase B2 (Admin Property Moderation API). Follows the same per-area
/// wrapper pattern <c>PropertyStoredProcedures</c>/<c>FraudStoredProcedures</c>
/// establish: inject <see cref="IStoredProcedureExecutor"/>, pass exact
/// stored-procedure parameter names as an anonymous object, map straight
/// into a plain DTO. No business logic lives here - both procedures'
/// final result set is <c>SELECT * FROM vw_PropertyListing WHERE
/// PropertyID = @PropertyID</c>, the exact same shape
/// <c>usp_Property_Create</c>/<c>usp_Property_Update</c> already return,
/// so this reuses the existing <see cref="PropertyListingResult"/> DTO
/// rather than inventing a new one.
/// </summary>
public class AdminStoredProcedures : IAdminStoredProcedures
{
    private readonly IStoredProcedureExecutor _executor;

    public AdminStoredProcedures(IStoredProcedureExecutor executor)
    {
        _executor = executor;
    }

    public async Task<PropertyListingResult> ApprovePropertyAsync(
        int adminId, int propertyId, string? remarks, CancellationToken cancellationToken = default)
    {
        var parameters = new { AdminID = adminId, PropertyID = propertyId, Remarks = remarks };

        var listing = await _executor.QuerySingleOrDefaultAsync<PropertyListingResult>(
            "dbo.usp_Admin_ApproveProperty", parameters, cancellationToken);

        return listing!;
    }

    public async Task<PropertyListingResult> RejectPropertyAsync(
        int adminId, int propertyId, string? remarks, CancellationToken cancellationToken = default)
    {
        var parameters = new { AdminID = adminId, PropertyID = propertyId, Remarks = remarks };

        var listing = await _executor.QuerySingleOrDefaultAsync<PropertyListingResult>(
            "dbo.usp_Admin_RejectProperty", parameters, cancellationToken);

        return listing!;
    }
}
