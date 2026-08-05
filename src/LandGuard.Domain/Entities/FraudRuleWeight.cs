namespace LandGuard.Domain.Entities;

/// <summary>
/// Maps to <c>dbo.FraudRuleWeight</c> [ext] - the configurable weight and
/// threshold for each of the 7 fraud rules, so the engine can be re-tuned
/// (<c>usp_Admin_UpdateRuleWeight</c>) without redeploying the API.
/// Weights total 100, enforced by convention (and a warning printed from
/// the procedure) rather than a CHECK constraint, since a mid-retune total
/// can briefly exceed 100 while an admin is editing multiple rules.
///
/// <see cref="RuleCode"/> is the primary key - a natural string key, not
/// an identity column - matching <c>PK_FraudRuleWeight</c> exactly. Reads
/// via LINQ are fine; writes should go through
/// <c>usp_Admin_UpdateRuleWeight</c> so the optional re-analysis of every
/// listing happens atomically with the weight change.
/// </summary>
public class FraudRuleWeight
{
    /// <summary>e.g. "PRICE_ANOMALY", "NIC_VERIFICATION" - matches dbo.vw_FraudCheckDetail.RuleCode.</summary>
    public string RuleCode { get; set; } = null!;

    public string RuleName { get; set; } = null!;

    /// <summary>Points added to the risk score when this rule fires.</summary>
    public int Weight { get; set; }

    /// <summary>Rule-specific tuning value (e.g. 0.40 for the 40% price-anomaly margin). Not every rule uses one.</summary>
    public decimal? Threshold { get; set; }

    /// <summary>False disables the rule - it contributes 0 regardless of Weight.</summary>
    public bool IsEnabled { get; set; }

    /// <summary>Shown to buyers/sellers in the fraud report.</summary>
    public string? Description { get; set; }
}
