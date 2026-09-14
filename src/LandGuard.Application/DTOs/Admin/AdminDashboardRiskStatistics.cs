namespace LandGuard.Application.DTOs.Admin;

/// <summary>
/// Legacy numeric fraud/risk-engine section of
/// <see cref="AdminFraudDashboardResponse"/> - sourced from
/// <c>dbo.vw_FraudStatistics</c>, the same weighted-rule engine
/// <c>FraudDetectionService</c> reports per-property (<c>FraudCheck</c>/
/// <c>RiskReport</c>). Deliberately a SEPARATE concept from
/// <see cref="AdminDashboardDeedVerificationStatistics"/> - High risk does
/// not mean Fraudulent, and Low risk does not mean Verified; see this
/// project's own "Important Semantics" note on why the two systems are
/// never combined.
/// </summary>
public class AdminDashboardRiskStatistics
{
    public int Low { get; set; }

    public int Medium { get; set; }

    public int High { get; set; }

    /// <summary>Null if no listing has been analysed yet - mirrors <c>FraudStatistics.AverageRiskScore</c>.</summary>
    public decimal? AverageRiskScore { get; set; }
}
