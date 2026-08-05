namespace LandGuard.Domain.Enums;

/// <summary>
/// Outcome label written by the fraud engine onto <c>dbo.FraudCheck.FraudStatus</c>
/// (<c>CK_FraudCheck_Status</c>). Derived from the same risk band as
/// RiskLevel but expressed as a verdict rather than a badge colour: Low
/// risk -&gt; Clean, Medium -&gt; Suspicious, High -&gt; Fraudulent. Kept as its
/// own enum (rather than reusing RiskLevel) because the two columns serve
/// different audiences - RiskLevel is the buyer-facing badge, FraudStatus
/// is the seller/admin-facing verdict - and the database itself stores
/// them as two independently constrained columns.
/// </summary>
public enum FraudStatus
{
    Clean = 1,
    Suspicious = 2,
    Fraudulent = 3
}
