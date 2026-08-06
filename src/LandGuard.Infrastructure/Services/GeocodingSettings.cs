namespace LandGuard.Infrastructure.Services;

/// <summary>
/// Strongly-typed binding of the "Geocoding" configuration section, bound
/// via <c>services.Configure&lt;GeocodingSettings&gt;</c> in
/// Infrastructure's DependencyInjection and used to configure the
/// <see cref="NominatimGeocodingService"/> HttpClient. Defaults point at
/// the free public Nominatim instance, which is fine for this FYP's local
/// development/demo use but is rate-limited (1 request/second, a
/// descriptive User-Agent required) - swap <see cref="BaseUrl"/> for a
/// self-hosted or commercial instance before any real production traffic.
/// </summary>
public class GeocodingSettings
{
    public string BaseUrl { get; set; } = "https://nominatim.openstreetmap.org";

    /// <summary>
    /// Required by Nominatim's usage policy - must identify the
    /// application (and ideally a contact method) so misbehaving clients
    /// can be reached before being blocked.
    /// </summary>
    public string UserAgent { get; set; } = "LandGuard-FYP/1.0";

    public int TimeoutSeconds { get; set; } = 5;
}
