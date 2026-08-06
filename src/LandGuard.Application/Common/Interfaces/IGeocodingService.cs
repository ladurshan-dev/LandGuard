using LandGuard.Application.Common.Models;

namespace LandGuard.Application.Common.Interfaces;

/// <summary>
/// Abstraction over resolving a free-text location into coordinates, so
/// PropertyService never references a specific geocoding provider
/// directly - the same Dependency Inversion pattern as every other
/// external concern in this solution. Implemented in Infrastructure
/// against the public Nominatim (OpenStreetMap) API.
/// </summary>
public interface IGeocodingService
{
    /// <summary>
    /// Attempts to resolve <paramref name="query"/> (typically "Location,
    /// District, Sri Lanka") to coordinates. Returns null - never throws -
    /// on no match, a network failure, or a timeout: geocoding is a
    /// best-effort enrichment, not a hard dependency of listing creation,
    /// and a null result correctly lets fraud rule 6 (Location Validation)
    /// flag the listing rather than blocking the submission outright.
    /// </summary>
    Task<GeocodingResult?> GeocodeAsync(string query, CancellationToken cancellationToken = default);
}
