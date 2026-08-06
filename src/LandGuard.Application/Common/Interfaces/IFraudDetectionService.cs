using LandGuard.Application.Common.Models;
using LandGuard.Application.DTOs.Fraud;

namespace LandGuard.Application.Common.Interfaces;

/// <summary>
/// Service Layer contract for Module 5A (Fraud Detection Foundation).
/// FraudController depends only on this interface, never on
/// FraudDetectionService directly or on any of the stored-procedure
/// abstractions it composes - the same shape IAuthService/IPropertyService
/// established.
///
/// This is deliberately a thin orchestration layer over the fraud engine
/// Module 2 already built entirely in T-SQL
/// (<c>usp_Fraud_AnalyseProperty</c> / <c>usp_Risk_GenerateReport</c> /
/// <c>dbo.FraudCheck</c> / <c>dbo.RiskReport</c> / <c>dbo.FraudRuleWeight</c>),
/// already wired into Property Create/Update by Module 4. No rule is
/// evaluated, and no score is calculated, in C# anywhere in this
/// interface's implementation - every method either triggers the existing
/// procedure or reads what it already computed.
///
/// <c>callerId</c>/<c>callerRole</c> parameters (absent from the
/// originally sketched <c>AnalyzePropertyAsync(propertyId)</c>-only
/// signature) are required on every method to enforce the specified
/// authorization model: a Buyer may only read, a Seller may only analyze
/// their own properties, an Admin has full access - see
/// FraudDetectionService's doc comment for exactly how each method
/// enforces this.
///
/// Every method returns a <see cref="Result"/>/<see cref="Result{T}"/> for
/// expected outcomes (not found, not the owner, an inactive seller
/// account) - the same pattern every other service in this solution uses.
/// </summary>
public interface IFraudDetectionService
{
    /// <summary>
    /// Triggers usp_Fraud_AnalyseProperty (via
    /// IPropertyStoredProcedures.AnalyseAsync, unchanged from Module 4)
    /// and returns the resulting score and rule breakdown. Validates the
    /// property exists, the caller owns it (or is an Admin), and the
    /// owning seller's account is active before triggering.
    /// </summary>
    Task<Result<FraudAnalysisResponse>> AnalyzePropertyAsync(
        int propertyId, int callerId, string? callerRole, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the current stored risk score for a property - never
    /// recomputes it. Subject to the same visibility rule as
    /// GetFraudReportAsync.
    /// </summary>
    Task<Result<RiskSummaryResponse>> CalculateRiskScoreAsync(
        int propertyId, int? callerId, string? callerRole, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the latest analysis result (score + rule breakdown) without
    /// re-running the engine. Visible to anyone once the property is
    /// Approved; otherwise only to the owning Seller or an Admin.
    /// </summary>
    Task<Result<FraudReportResponse>> GetFraudReportAsync(
        int propertyId, int? callerId, string? callerRole, CancellationToken cancellationToken = default);

    /// <summary>Every past analysis run for a property (usp_Fraud_GetHistory), subject to the same visibility rule as GetFraudReportAsync.</summary>
    Task<Result<FraudHistoryResponse>> GetFraudHistoryAsync(
        int propertyId, int? callerId, string? callerRole, CancellationToken cancellationToken = default);
}
