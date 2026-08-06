namespace LandGuard.Application.Common.Models;

/// <summary>
/// Coordinates resolved from a free-text location string by
/// <see cref="Interfaces.IGeocodingService"/>. <c>dbo.Property</c> docs
/// this exactly: "Latitude/Longitude - Written back from the Nominatim
/// API". Fraud rule 6 (Location Validation, inside
/// <c>usp_Fraud_AnalyseProperty</c>) fires when these are missing or fall
/// outside Sri Lanka's bounding box, so a failed/empty geocode is not an
/// error here - it is passed through as null coordinates and the engine
/// correctly flags it.
/// </summary>
public record GeocodingResult(decimal Latitude, decimal Longitude);
