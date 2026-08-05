namespace LandGuard.Domain.ReadModels;

/// <summary>
/// Keyless read model mapped to <c>dbo.vw_RuleTriggerFrequency</c> - how
/// often each of the 7 rules fires across every analysed listing, the
/// evidence an admin uses to decide whether a rule's weight/threshold
/// needs retuning via <c>usp_Admin_UpdateRuleWeight</c>. One row per rule
/// in <c>dbo.FraudRuleWeight</c>, including rules that have never fired.
/// </summary>
public class RuleTriggerFrequency
{
    public string RuleCode { get; set; } = null!;

    public string RuleName { get; set; } = null!;

    public int Weight { get; set; }

    public int TimesTriggered { get; set; }

    public int TimesEvaluated { get; set; }

    /// <summary>Null if TimesEvaluated is 0 (NULLIF guards the divide in the view).</summary>
    public decimal? TriggerRatePercent { get; set; }
}
