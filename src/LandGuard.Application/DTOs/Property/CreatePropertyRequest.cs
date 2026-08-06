namespace LandGuard.Application.DTOs.Property;

/// <summary>
/// POST /api/properties. SellerId always comes from the caller's JWT
/// (ICurrentUserService), never from this body - the same rule
/// Module 3's ChangePasswordRequest established. Latitude/Longitude are
/// optional overrides for a frontend that lets the seller pin an exact
/// point on a map; when omitted, PropertyService geocodes
/// Location/District via IGeocodingService instead.
/// </summary>
public class CreatePropertyRequest
{
    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public string Location { get; set; } = null!;

    public string? District { get; set; }

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    /// <summary>Land size in perches.</summary>
    public double Size { get; set; }

    /// <summary>Asking price in LKR.</summary>
    public decimal Price { get; set; }

    public string? DeedReference { get; set; }
}
