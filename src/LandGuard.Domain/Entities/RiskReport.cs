using LandGuard.Domain.Enums;

namespace LandGuard.Domain.Entities;

/// <summary>
/// Maps to <c>dbo.RiskReport</c> - point 8 of the fraud engine (the
/// combined weighted score), 1:1 with <see cref="FraudCheck"/>.
///
/// Deliberately has no <c>PropertyId</c> property: the database does not
/// store one either (it is reachable transitively through
/// <see cref="FraudCheckId"/>), because duplicating it would reintroduce
/// the transitive dependency 3NF removes. Written exclusively by
/// <c>usp_Risk_GenerateReport</c>.
/// </summary>
public class RiskReport
{
    public int ReportId { get; set; }

    public int FraudCheckId { get; set; }

    /// <summary>0-100, sum of the weights of every rule that fired (CK_RiskReport_Score).</summary>
    public int RiskScore { get; set; }

    /// <summary>Low 0-40 / Medium 41-70 / High 71-100 (CK_RiskReport_Banding).</summary>
    public RiskLevel RiskLevel { get; set; }

    /// <summary>Human-readable fraud report shown to buyers and sellers (FR06).</summary>
    public string? Summary { get; set; }

    public DateTime GeneratedDate { get; set; }

    public FraudCheck FraudCheck { get; set; } = null!;
}
