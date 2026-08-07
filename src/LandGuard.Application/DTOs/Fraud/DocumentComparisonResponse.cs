namespace LandGuard.Application.DTOs.Fraud;

/// <summary>
/// POST /api/fraud/compare/{propertyId} and GET
/// /api/fraud/comparison/{propertyId}'s response - the top-level shape
/// Module 5C's two endpoints actually return. Wraps
/// <see cref="ComparisonResultResponse"/> (the comparison run itself) with
/// property/document context and, alongside it, the property's *current*
/// fraud risk as already computed by the existing Module 5A engine - see
/// <c>DocumentComparisonService</c>'s doc comment for why this is how
/// Module 5C "sends results to the existing Fraud Detection Foundation"
/// without modifying or duplicating it.
/// </summary>
public class DocumentComparisonResponse
{
    public int PropertyId { get; set; }

    /// <summary>The storage reference of the document this comparison was run against, if the caller supplied one (see Module 5B's OcrResultResponse.DocumentReference).</summary>
    public string? DocumentReference { get; set; }

    public ComparisonResultResponse Result { get; set; } = null!;

    /// <summary>
    /// The property's current risk, read (never recalculated) from the
    /// existing Module 5A fraud engine via
    /// <c>IFraudDetectionService.CalculateRiskScoreAsync</c> - null only if
    /// that read itself failed (e.g. a visibility check), which should not
    /// normally happen given this response was already built.
    /// </summary>
    public RiskSummaryResponse? CurrentFraudRisk { get; set; }
}
