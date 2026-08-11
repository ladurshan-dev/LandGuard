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

    /// <summary>Mandatory - see CreatePropertyRequestValidator.</summary>
    public string DeedReference { get; set; } = null!;

    /// <summary>The deed's registered owner name - mandatory, explicit deed-owner data distinct from the Seller account's own Name. See LandGuard.Domain.Entities.Property.OwnerName's doc comment.</summary>
    public string OwnerName { get; set; } = null!;

    /// <summary>The deed's registered owner NIC - mandatory. Sri Lankan NIC format, same pattern as Auth's own NIC validation (AuthValidationRules.NicPattern).</summary>
    public string OwnerNic { get; set; } = null!;

    /// <summary>The deed's registered owner address - mandatory.</summary>
    public string OwnerAddress { get; set; } = null!;
}
