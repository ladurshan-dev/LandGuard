namespace LandGuard.Application.Common.Models;

/// <summary>
/// One row of the third result set of <c>usp_Property_GetById</c> (from
/// <c>dbo.vw_FraudCheckDetail</c>) - the rule-by-rule breakdown of the
/// latest fraud analysis run, shown to a Buyer as the listing's fraud
/// report (FR06) and to the owning Seller as the reason list for a
/// Flagged/Rejected listing. Always exactly 7 rows once the engine has run
/// at least once (one per rule); empty for a listing that predates any
/// analysis, which should not happen in practice since Create/Update both
/// trigger the engine synchronously.
/// </summary>
public class PropertyFraudRuleResult
{
    /// <summary>e.g. "PRICE_ANOMALY" - matches dbo.FraudRuleWeight.RuleCode.</summary>
    public string RuleCode { get; set; } = null!;

    public string RuleName { get; set; } = null!;

    public bool Triggered { get; set; }

    /// <summary>The rule's weight if it fired, otherwise 0.</summary>
    public int PointsAdded { get; set; }

    /// <summary>The rule's configured weight regardless of whether it fired - lets the UI show a "12 / 20" style bar.</summary>
    public int MaxPoints { get; set; }

    public string? Description { get; set; }
}
