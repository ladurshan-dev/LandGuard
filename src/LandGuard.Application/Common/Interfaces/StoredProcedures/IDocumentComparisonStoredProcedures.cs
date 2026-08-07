using LandGuard.Application.Common.Models;

namespace LandGuard.Application.Common.Interfaces.StoredProcedures;

/// <summary>
/// Application-layer contract over Module 5C's two new stored procedures
/// (<c>usp_DocumentComparison_Save</c>, <c>usp_DocumentComparison_GetLatest</c>
/// - see <c>database/Module5C_DocumentComparison.sql</c>). Implemented in
/// Infrastructure using Dapper (see <c>DocumentComparisonStoredProcedures</c>),
/// following exactly the shape <c>IFraudStoredProcedures</c>/
/// <c>IPropertyStoredProcedures</c> established.
/// </summary>
public interface IDocumentComparisonStoredProcedures
{
    /// <summary>
    /// Wraps usp_DocumentComparison_Save. Persists one comparison run
    /// (header + field rows, in a single transaction/round trip via a
    /// table-valued parameter) and returns it back as saved, including the
    /// generated ComparisonID.
    /// </summary>
    Task<DocumentComparisonRecord> SaveAsync(
        int propertyId,
        int comparedByUserId,
        string? documentReference,
        decimal overallMatchPercentage,
        string? summary,
        IReadOnlyList<DocumentComparisonFieldRow> fields,
        CancellationToken cancellationToken = default);

    /// <summary>Wraps usp_DocumentComparison_GetLatest. Null if the property has never been compared.</summary>
    Task<DocumentComparisonRecord?> GetLatestAsync(int propertyId, CancellationToken cancellationToken = default);
}
