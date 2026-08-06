namespace LandGuard.Application.DTOs.Fraud;

/// <summary>
/// GET /api/fraud/report/{propertyId}'s response - the latest analysis
/// result for a property, without re-running the engine. Visibility
/// follows the same rule <c>PropertyService.GetByIdAsync</c> already
/// enforces (public once Approved; owner/Admin only otherwise), since
/// this is built directly from that same call.
/// </summary>
public class FraudReportResponse
{
    public int PropertyId { get; set; }

    public string PropertyTitle { get; set; } = null!;

    /// <summary>"Pending" | "Approved" | "Flagged" | "Rejected".</summary>
    public string PropertyStatus { get; set; } = null!;

    public RiskSummaryResponse Risk { get; set; } = null!;

    public IReadOnlyList<FraudRuleResponse> Rules { get; set; } = Array.Empty<FraudRuleResponse>();
}
