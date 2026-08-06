namespace LandGuard.Application.DTOs.Fraud;

/// <summary>
/// The combined score (point 8 of the fraud engine) for one analysis run -
/// nested inside <see cref="FraudAnalysisResponse"/>,
/// <see cref="FraudReportResponse"/> and each entry of
/// <see cref="FraudHistoryResponse"/>. Always a direct read of
/// <c>dbo.RiskReport</c> (via <c>vw_PropertyLatestRisk</c>/
/// <c>vw_PropertyListing</c> or, for history, the row itself) - the score
/// is computed exactly once, by <c>usp_Risk_GenerateReport</c> inside the
/// database, and never recomputed in C#.
/// </summary>
public class RiskSummaryResponse
{
    /// <summary>0-100 (CK_RiskReport_Score) - sum of the weights of every rule that fired.</summary>
    public int RiskScore { get; set; }

    /// <summary>"Low" (0-40) | "Medium" (41-70) | "High" (71-100) - CK_RiskReport_Banding.</summary>
    public string RiskLevel { get; set; } = null!;

    /// <summary>"Clean" | "Suspicious" | "Fraudulent".</summary>
    public string FraudStatus { get; set; } = null!;

    /// <summary>Human-readable summary written by usp_Risk_GenerateReport (FR06).</summary>
    public string? Summary { get; set; }

    public DateTime? GeneratedDate { get; set; }
}
