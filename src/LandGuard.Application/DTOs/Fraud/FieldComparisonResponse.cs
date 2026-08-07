namespace LandGuard.Application.DTOs.Fraud;

/// <summary>
/// One field's comparison outcome, nested inside
/// <see cref="ComparisonResultResponse"/>. Field-for-field mirror of
/// <c>Common.Models.DocumentComparisonFieldRow</c> - a distinct API DTO
/// still exists here (rather than returning the Common.Models type
/// directly) to follow this solution's established rule that
/// Dapper-projection models and API response DTOs are never the same
/// type, even when their shapes currently match exactly.
/// </summary>
public class FieldComparisonResponse
{
    /// <summary>e.g. "OwnerName", "NIC", "District".</summary>
    public string FieldName { get; set; } = null!;

    /// <summary>The value extracted from the deed by OCR (Module 5B), as supplied in the request.</summary>
    public string? OcrValue { get; set; }

    /// <summary>The corresponding value read from LandGuardDB, or null when this field has no database counterpart to compare against.</summary>
    public string? DatabaseValue { get; set; }

    public bool Matched { get; set; }

    /// <summary>0-100.</summary>
    public decimal SimilarityPercentage { get; set; }

    /// <summary>Human-readable explanation of the outcome above - always populated, even for a match.</summary>
    public string Message { get; set; } = null!;
}
