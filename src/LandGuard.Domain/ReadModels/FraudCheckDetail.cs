namespace LandGuard.Domain.ReadModels;

/// <summary>
/// Keyless read model mapped to <c>dbo.vw_FraudCheckDetail</c> - the
/// row-per-rule breakdown of the 8-point engine used to render the fraud
/// report shown to buyers (FR06) and the reason list shown to sellers.
/// One row per rule (7 rows per property), joined against
/// <c>dbo.FraudRuleWeight</c> for the display name/weight/description.
/// </summary>
public class FraudCheckDetail
{
    public int PropertyId { get; set; }

    public int FraudCheckId { get; set; }

    /// <summary>e.g. "PRICE_ANOMALY" - matches dbo.FraudRuleWeight.RuleCode.</summary>
    public string RuleCode { get; set; } = null!;

    public string RuleName { get; set; } = null!;

    public bool Triggered { get; set; }

    /// <summary>Weight if Triggered, otherwise 0.</summary>
    public int PointsAdded { get; set; }

    /// <summary>The rule's configured weight, regardless of whether it fired - lets the UI show "12 / 20" style bars.</summary>
    public int MaxPoints { get; set; }

    public string? Description { get; set; }
}
