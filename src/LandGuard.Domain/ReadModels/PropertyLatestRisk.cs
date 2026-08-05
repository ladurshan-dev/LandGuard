namespace LandGuard.Domain.ReadModels;

/// <summary>
/// Keyless read model mapped to <c>dbo.vw_PropertyLatestRisk</c> - the
/// most recent fraud check + risk report per property, and the base every
/// other risk-aware view builds on.
///
/// Design note (applies to every class in this folder): status/level
/// columns here are plain strings, not the Domain enums used on the base
/// entities (<c>PropertyStatus</c>, <c>FraudStatus</c>, <c>RiskLevel</c>).
/// Views are read-only projections with their own <c>ISNULL(...)</c>
/// defaults and joined shapes that don't line up 1:1 with a single table's
/// CHECK constraint, so rather than force an enum conversion that could
/// silently fail on an edge case, these read models mirror exactly what
/// the SQL returns. Callers that need the strongly-typed enum can parse
/// it (the string values are identical to the enum member names, except
/// where noted).
/// </summary>
public class PropertyLatestRisk
{
    public int PropertyId { get; set; }

    public int FraudCheckId { get; set; }

    public bool PriceCheck { get; set; }

    public bool DuplicateCheck { get; set; }

    public bool NicCheck { get; set; }

    public bool DeedCheck { get; set; }

    public bool SellerHistoryCheck { get; set; }

    public bool LocationCheck { get; set; }

    public bool MissingInfoCheck { get; set; }

    /// <summary>"Clean" | "Suspicious" | "Fraudulent".</summary>
    public string FraudStatus { get; set; } = null!;

    public DateTime CheckDate { get; set; }

    /// <summary>Null only if the FraudCheck row exists but usp_Risk_GenerateReport has not yet run for it.</summary>
    public int? ReportId { get; set; }

    public int? RiskScore { get; set; }

    /// <summary>"Low" | "Medium" | "High".</summary>
    public string? RiskLevel { get; set; }

    public string? Summary { get; set; }

    public DateTime? GeneratedDate { get; set; }
}
