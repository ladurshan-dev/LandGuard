namespace LandGuard.Application.Common.Models;

/// <summary>
/// Composite of all 3 result sets <c>usp_Property_GetById</c> returns -
/// assembled once in <c>PropertyStoredProcedures.GetByIdAsync</c> (the only
/// place a <c>SqlMapper.GridReader</c> is ever touched) so every layer
/// above Infrastructure sees one plain object instead of three separately
/// ordered result sets it would otherwise have to know how to read.
/// </summary>
public class PropertyDetail
{
    public PropertyListingResult Listing { get; set; } = null!;

    public IReadOnlyList<PropertyImageSummary> Images { get; set; } = Array.Empty<PropertyImageSummary>();

    public IReadOnlyList<PropertyFraudRuleResult> FraudReport { get; set; } = Array.Empty<PropertyFraudRuleResult>();
}
