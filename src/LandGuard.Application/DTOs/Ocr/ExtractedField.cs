namespace LandGuard.Application.DTOs.Ocr;

/// <summary>
/// One placeholder-parsed field from a deed document's OCR text (Owner
/// Name, NIC, Property Address, Parcel Number, Registration Number,
/// Survey Plan Number, Land Extent, District, Province, Date, plus
/// PropertyReference/RegisteredPrice/Status added additively for the
/// Government Registry module's Phase 4 deed comparison). Produced by
/// simple label/regex heuristics (<c>Services.DocumentFieldExtractor</c>),
/// not a trained model - deliberately so per Module 5B's original scope
/// (no AI). <c>GovernmentDeedComparisonService</c> is what turns this list
/// into the normalized <c>SellerDeedData</c>/<c>GovernmentDeedData</c>
/// shapes <c>DeedFieldComparer</c> actually compares.
/// </summary>
public class ExtractedField
{
    /// <summary>e.g. "OwnerName", "NIC", "District" - stable identifier Module 5C can key off.</summary>
    public string FieldName { get; set; } = null!;

    /// <summary>The extracted value, or null if this field's pattern/label wasn't found in the text.</summary>
    public string? Value { get; set; }

    /// <summary>False when Value is null - kept as an explicit flag (rather than relying on callers to null-check) so "not found" is unambiguous even for a field whose value could legitimately be an empty-looking string.</summary>
    public bool Found { get; set; }
}
