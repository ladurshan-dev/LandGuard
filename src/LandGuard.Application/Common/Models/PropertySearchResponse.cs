namespace LandGuard.Application.Common.Models;

/// <summary>
/// The paged envelope <c>GET /api/properties</c> returns - built by
/// <c>PropertyService</c> from <see cref="PropertySearchResult"/> rows
/// (reading <c>TotalRecords</c> off the first row, 0 if there are none)
/// rather than exposing the "every row repeats the total" shape the
/// procedure uses internally directly to API clients.
/// </summary>
public class PropertySearchResponse
{
    public IReadOnlyList<PropertySearchResult> Items { get; set; } = Array.Empty<PropertySearchResult>();

    public int TotalRecords { get; set; }

    public int PageNumber { get; set; }

    public int PageSize { get; set; }
}
