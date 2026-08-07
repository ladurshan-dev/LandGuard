namespace LandGuard.Application.Common.Models;

/// <summary>
/// Composite of both result sets <c>usp_DocumentComparison_Save</c> and
/// <c>usp_DocumentComparison_GetLatest</c> return - assembled once in
/// <c>DocumentComparisonStoredProcedures</c> (the only place a
/// <c>SqlMapper.GridReader</c> is touched for this feature), mirroring how
/// <c>PropertyStoredProcedures.GetByIdAsync</c> assembles
/// <see cref="PropertyDetail"/> from <c>usp_Property_GetById</c>'s 3 result
/// sets.
/// </summary>
public class DocumentComparisonRecord
{
    public DocumentComparisonHeader Header { get; set; } = null!;

    public IReadOnlyList<DocumentComparisonFieldRow> Fields { get; set; } = Array.Empty<DocumentComparisonFieldRow>();
}
