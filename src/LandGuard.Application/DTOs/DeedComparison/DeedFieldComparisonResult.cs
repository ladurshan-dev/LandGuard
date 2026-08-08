namespace LandGuard.Application.DTOs.DeedComparison;

/// <summary>
/// One field's outcome in a <see cref="GovernmentDeedComparisonReport"/> -
/// intentionally echoes <c>DTOs.Fraud.FraudRuleResponse</c>'s shape
/// (a code-like name, a pass/fail flag, a human-readable message) since
/// this is conceptually the same kind of "did this check pass" result,
/// just produced by <c>Services.DeedFieldComparer</c> rather than
/// <c>usp_Fraud_AnalyseProperty</c>. Kept as its own type rather than
/// reusing <c>FraudRuleResponse</c> directly: this result carries the two
/// actual compared values (useful for a human reviewer), which no fraud
/// rule result exposes, and nothing here is written to
/// <c>dbo.FraudCheck</c> - see <c>GovernmentDeedComparisonReport</c>'s doc
/// comment for why this stays a separate report in Phase 4.
/// </summary>
public class DeedFieldComparisonResult
{
    /// <summary>e.g. "NIC", "DeedNumber", "LandSize", "Price" - stable identifier a caller can key off.</summary>
    public string FieldName { get; set; } = null!;

    public string? GovernmentValue { get; set; }

    public string? SellerValue { get; set; }

    /// <summary>True if the two values agree (within tolerance, for LandSize/Price - see DeedFieldComparer) or if the field could not be evaluated because a value was missing on either side.</summary>
    public bool Match { get; set; }

    /// <summary>Human-readable explanation, always populated - notably for Price, always states the asking-price-vs-registered-price distinction regardless of outcome (see DeedFieldComparer).</summary>
    public string Message { get; set; } = null!;
}
