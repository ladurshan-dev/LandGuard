namespace LandGuard.Application.DTOs.Fraud;

/// <summary>One entry of <see cref="FraudHistoryResponse"/> - a single past analysis run's score, without its per-rule breakdown (only the latest run's breakdown is available - see FraudReportResponse).</summary>
public class FraudHistoryEntryResponse
{
    public int FraudCheckId { get; set; }

    public DateTime CheckDate { get; set; }

    public RiskSummaryResponse Risk { get; set; } = null!;
}

/// <summary>GET /api/fraud/history/{propertyId}'s response - every past analysis run, newest first (usp_Fraud_GetHistory, Module 5A's one new stored procedure).</summary>
public class FraudHistoryResponse
{
    public int PropertyId { get; set; }

    public IReadOnlyList<FraudHistoryEntryResponse> Runs { get; set; } = Array.Empty<FraudHistoryEntryResponse>();
}
