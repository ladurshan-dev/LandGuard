namespace LandGuard.Application.DTOs.Fraud;

/// <summary>
/// One rule's outcome in the latest analysis run - a direct projection of
/// <c>dbo.vw_FraudCheckDetail</c> (via
/// <c>Common.Models.PropertyFraudRuleResult</c>), never recomputed here.
/// Always exactly 7 of these once a property has been analysed at least
/// once, one per row in <c>dbo.FraudRuleWeight</c>.
/// </summary>
public class FraudRuleResponse
{
    /// <summary>e.g. "PRICE_ANOMALY" - matches dbo.FraudRuleWeight.RuleCode.</summary>
    public string RuleCode { get; set; } = null!;

    public string RuleName { get; set; } = null!;

    /// <summary>Points this rule contributes to RiskScore when it fires (dbo.FraudRuleWeight.Weight).</summary>
    public int Weight { get; set; }

    /// <summary>True if the rule did NOT fire (no fraud indicator detected); false if it fired.</summary>
    public bool Passed { get; set; }

    /// <summary>Rule description shown to Buyers/Sellers (dbo.FraudRuleWeight.Description).</summary>
    public string? Message { get; set; }
}
