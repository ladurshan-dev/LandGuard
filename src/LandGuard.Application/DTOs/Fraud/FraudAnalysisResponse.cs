namespace LandGuard.Application.DTOs.Fraud;

/// <summary>
/// POST /api/fraud/analyze/{propertyId}'s response - the outcome of
/// triggering <c>usp_Fraud_AnalyseProperty</c> (via
/// <c>IPropertyStoredProcedures.AnalyseAsync</c>, unchanged from Module 4)
/// against a property, re-read immediately after the run completes.
/// </summary>
public class FraudAnalysisResponse
{
    public int PropertyId { get; set; }

    /// <summary>"Pending" | "Approved" | "Flagged" | "Rejected" - as usp_Risk_GenerateReport left it after this run.</summary>
    public string PropertyStatus { get; set; } = null!;

    public RiskSummaryResponse Risk { get; set; } = null!;

    public IReadOnlyList<FraudRuleResponse> Rules { get; set; } = Array.Empty<FraudRuleResponse>();
}
