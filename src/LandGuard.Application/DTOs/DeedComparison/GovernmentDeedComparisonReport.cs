namespace LandGuard.Application.DTOs.DeedComparison;

/// <summary>
/// The result of comparing a seller's uploaded deed against the trusted
/// government record for a property (Government Registry module, Phase
/// 4) - returned directly to the caller by
/// <c>GovernmentDeedComparisonService</c>, never persisted.
///
/// Deliberately NOT merged into <c>dbo.FraudCheck</c>/<c>dbo.RiskReport</c>
/// or <c>DTOs.Fraud.FraudReportResponse</c>: <c>FraudCheck</c> has a fixed
/// 7-boolean-column shape written exclusively by
/// <c>usp_Fraud_AnalyseProperty</c>, which has no visibility into
/// <c>IGovernmentRegistryService</c>'s data (dummy today, a real external
/// API later) - there is no way to write this outcome into that table
/// without a schema change, which Phase 4 is explicitly scoped not to
/// make. Folding a government-verification outcome into the numeric
/// RiskScore is left for a later phase (adding one new, additive
/// <c>FraudRuleWeight</c> row and one new <c>FraudCheck</c> column, with
/// <c>usp_Fraud_AnalyseProperty</c> accepting the precomputed outcome as a
/// new parameter - the engine still evaluates every rule itself, this
/// service just becomes one more input to it, not a second scoring
/// system). Until then, this report stands alongside the existing fraud
/// report, not inside it.
/// </summary>
public class GovernmentDeedComparisonReport
{
    public int PropertyId { get; set; }

    /// <summary>The resolved GovernmentLandRecordDto.RecordId, or null if no government record could be resolved at all (Scenario F).</summary>
    public string? GovernmentRecordId { get; set; }

    public bool GovernmentRecordFound { get; set; }

    /// <summary>"Active" | "Cancelled" | "Suspended" | null (no record found at all).</summary>
    public string? GovernmentRecordStatus { get; set; }

    /// <summary>"Clean" | "Mismatch" | "MissingOrCancelledGovernmentRecord".</summary>
    public string OverallOutcome { get; set; } = null!;

    /// <summary>Empty when OverallOutcome is "MissingOrCancelledGovernmentRecord" - there is nothing reliable to diff a field-by-field breakdown against.</summary>
    public IReadOnlyList<DeedFieldComparisonResult> Fields { get; set; } = Array.Empty<DeedFieldComparisonResult>();

    public DateTime GeneratedDate { get; set; }
}
