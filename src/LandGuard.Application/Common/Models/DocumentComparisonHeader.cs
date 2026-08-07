namespace LandGuard.Application.Common.Models;

/// <summary>
/// Shape of <c>dbo.DocumentComparison</c>'s row - returned as the first
/// result set of both <c>usp_DocumentComparison_Save</c> and
/// <c>usp_DocumentComparison_GetLatest</c>.
/// </summary>
public class DocumentComparisonHeader
{
    public int ComparisonId { get; set; }

    public int PropertyId { get; set; }

    public int ComparedByUserId { get; set; }

    public string? DocumentReference { get; set; }

    public int FieldsCompared { get; set; }

    public int FieldsMatched { get; set; }

    public decimal OverallMatchPercentage { get; set; }

    public string? Summary { get; set; }

    public DateTime ComparisonDate { get; set; }
}
