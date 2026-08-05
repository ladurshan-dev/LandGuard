namespace LandGuard.Domain.Enums;

/// <summary>
/// Human-facing classification derived from the Fraud Detection Engine's
/// numeric risk score (FR05 banding). Redefined in Module 2 to the three
/// values actually enforced by <c>CK_RiskReport_Banding</c> in
/// <c>dbo.RiskReport</c> - Low 0-40, Medium 41-70, High 71-100 - rather
/// than the four speculative levels (including an unused "Critical")
/// drafted in Module 1. The database itself is the source of truth for
/// banding: <c>dbo.fn_RiskLevelFromScore</c> computes it, and the CHECK
/// constraint refuses to store a score/level pair that disagrees.
/// </summary>
public enum RiskLevel
{
    Low = 1,
    Medium = 2,
    High = 3
}
