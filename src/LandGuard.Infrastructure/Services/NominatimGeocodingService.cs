using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using LandGuard.Application.Common.Interfaces;
using LandGuard.Application.Common.Models;
using Microsoft.Extensions.Logging;

namespace LandGuard.Infrastructure.Services;

/// <inheritdoc cref="IGeocodingService" />
public class NominatimGeocodingService : IGeocodingService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<NominatimGeocodingService> _logger;

    public NominatimGeocodingService(HttpClient httpClient, ILogger<NominatimGeocodingService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<GeocodingResult?> GeocodeAsync(string query, CancellationToken cancellationToken = default)
    {
        try
        {
            // countrycodes=lk narrows results to Sri Lanka, matching what
            // fraud rule 6 (Location Validation) actually checks the
            // coordinates against.
            var requestUri = $"search?q={Uri.EscapeDataString(query)}&format=json&limit=1&countrycodes=lk";

            var results = await _httpClient.GetFromJsonAsync<List<NominatimResult>>(requestUri, cancellationToken);
            var first = results?.FirstOrDefault();

            if (first is null
                || !decimal.TryParse(first.Lat, NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude)
                || !decimal.TryParse(first.Lon, NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude))
            {
                return null;
            }

            return new GeocodingResult(latitude, longitude);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            // Geocoding is best-effort enrichment, not a hard dependency -
            // a network failure, timeout, or unparsable response must never
            // block listing creation. Returning null here lets fraud rule 6
            // correctly flag the listing for missing coordinates instead,
            // which is the right outcome anyway (see IGeocodingService).
            _logger.LogWarning(ex, "Geocoding lookup failed for query '{Query}'", query);
            return null;
        }
    }

    /// <summary>Only the two fields this service reads from Nominatim's response array.</summary>
    private sealed class NominatimResult
    {
        [JsonPropertyName("lat")]
        public string Lat { get; set; } = null!;

        [JsonPropertyName("lon")]
        public string Lon { get; set; } = null!;
    }
}
