namespace LandGuard.Application.Common.Models;

/// <summary>
/// One row of <c>usp_Fraud_GetHistory</c> - one past analysis run
/// (<c>dbo.FraudCheck</c>) left-joined to its score
/// (<c>dbo.RiskReport</c>, nullable only in the narrow window between
/// <c>usp_Fraud_AnalyseProperty</c>'s insert and
/// <c>usp_Risk_GenerateReport</c>'s own insert - in practice always
/// populated by the time this is read, since both run inside the same
/// procedure call). A dedicated Dapper-projection DTO, not the Domain
/// <c>FraudCheck</c>/<c>RiskReport</c> entities, following the same
/// reasoning <c>NotificationSummary</c>/<c>UserProfile</c> established.
/// </summary>
public class FraudHistoryEntry
{
    public int FraudCheckId { get; set; }

    public DateTime CheckDate { get; set; }

    /// <summary>"Clean" | "Suspicious" | "Fraudulent".</summary>
    public string FraudStatus { get; set; } = null!;

    public bool PriceCheck { get; set; }

    public bool DuplicateCheck { get; set; }

    public bool NicCheck { get; set; }

    public bool DeedCheck { get; set; }

    public bool SellerHistoryCheck { get; set; }

    public bool LocationCheck { get; set; }

    public bool MissingInfoCheck { get; set; }

    public int? ReportId { get; set; }

    public int? RiskScore { get; set; }

    /// <summary>"Low" | "Medium" | "High" - null if no RiskReport row exists yet for this run.</summary>
    public string? RiskLevel { get; set; }

    public string? Summary { get; set; }

    public DateTime? GeneratedDate { get; set; }
}
