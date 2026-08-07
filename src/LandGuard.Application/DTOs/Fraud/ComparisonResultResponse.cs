namespace LandGuard.Application.DTOs.Fraud;

/// <summary>
/// One comparison run's own data - a direct read of
/// <c>dbo.DocumentComparison</c> plus its
/// <c>dbo.DocumentComparisonField</c> rows, nested inside
/// <see cref="DocumentComparisonResponse"/> (which adds property/document
/// context and the current fraud risk alongside it).
/// </summary>
public class ComparisonResultResponse
{
    public int ComparisonId { get; set; }

    public int FieldsCompared { get; set; }

    public int FieldsMatched { get; set; }

    /// <summary>Average of every field's SimilarityPercentage - 0-100.</summary>
    public decimal OverallMatchPercentage { get; set; }

    /// <summary>e.g. "8 of 10 fields matched (76.4% average similarity).".</summary>
    public string? Summary { get; set; }

    public DateTime ComparisonDate { get; set; }

    public IReadOnlyList<FieldComparisonResponse> Fields { get; set; } = Array.Empty<FieldComparisonResponse>();
}
