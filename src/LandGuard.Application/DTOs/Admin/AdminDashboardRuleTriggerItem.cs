namespace LandGuard.Application.DTOs.Admin;

/// <summary>
/// One row of <see cref="AdminFraudDashboardResponse.RuleTriggers"/> - a
/// direct, field-for-field projection of
/// <c>LandGuard.Domain.ReadModels.RuleTriggerFrequency</c>
/// (<c>dbo.vw_RuleTriggerFrequency</c>). No rule is recalculated in C#
/// here; every figure already comes from the view.
/// </summary>
public class AdminDashboardRuleTriggerItem
{
    public string RuleCode { get; set; } = null!;

    public string RuleName { get; set; } = null!;

    public int Weight { get; set; }

    public int TimesTriggered { get; set; }

    public int TimesEvaluated { get; set; }

    /// <summary>Null if TimesEvaluated is 0 - mirrors <c>RuleTriggerFrequency.TriggerRatePercent</c>.</summary>
    public decimal? TriggerRatePercent { get; set; }
}
