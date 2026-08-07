namespace LandGuard.Application.Common.Models;

/// <summary>
/// One compared field's result - shape matches
/// <c>dbo.DocumentComparisonFieldType</c>/<c>dbo.DocumentComparisonField</c>
/// column-for-column exactly, so this single type serves both directions:
/// <c>DocumentComparisonStoredProcedures.SaveAsync</c> writes a list of
/// these as the <c>@Fields</c> table-valued parameter, and
/// <c>usp_DocumentComparison_GetLatest</c>'s second result set is mapped
/// straight back into the same type. <see cref="Services.FieldComparer"/>
/// is what actually produces these.
/// </summary>
public class DocumentComparisonFieldRow
{
    /// <summary>e.g. "OwnerName", "NIC", "District" - the same FieldName values Module 5B's DocumentFieldExtractor/ExtractedField use.</summary>
    public string FieldName { get; set; } = null!;

    public string? OcrValue { get; set; }

    /// <summary>The corresponding value read from LandGuardDB, or null when no database field exists to compare against (see FieldComparer.NotAvailable).</summary>
    public string? DatabaseValue { get; set; }

    public bool Matched { get; set; }

    /// <summary>0-100.</summary>
    public decimal SimilarityPercentage { get; set; }

    public string? Message { get; set; }
}
