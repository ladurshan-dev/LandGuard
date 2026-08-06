using LandGuard.Application.Common.Models;

namespace LandGuard.Application.Common.Interfaces.StoredProcedures;

/// <summary>
/// Application-layer contract over the one genuinely new fraud-related
/// stored procedure Module 5A needed - <c>usp_Fraud_GetHistory</c>.
///
/// Deliberately minimal: triggering analysis already has a wrapper
/// (<see cref="IPropertyStoredProcedures.AnalyseAsync"/>, built in
/// Module 4 around <c>usp_Fraud_AnalyseProperty</c>) and reading the
/// current/latest report already has one too
/// (<see cref="IPropertyStoredProcedures.GetByIdAsync"/>, whose
/// <c>PropertyDetail.FraudReport</c> is exactly the rule-by-rule
/// breakdown). This interface does not re-declare either - consuming the
/// existing wrappers instead of duplicating them is the whole point of
/// Module 5A being a thin service layer over an already-complete T-SQL
/// fraud engine.
/// </summary>
public interface IFraudStoredProcedures
{
    /// <summary>Wraps usp_Fraud_GetHistory. Every past analysis run for a property, newest first.</summary>
    Task<IReadOnlyList<FraudHistoryEntry>> GetHistoryAsync(int propertyId, CancellationToken cancellationToken = default);
}
