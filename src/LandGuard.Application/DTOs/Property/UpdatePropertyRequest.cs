namespace LandGuard.Application.DTOs.Property;

/// <summary>
/// PUT /api/properties/{id}. Every field is optional, matching
/// usp_Property_Update's ISNULL(@Param, Column) pattern - only supplied
/// fields are changed. Updating always resets Status to "Pending" and
/// re-runs the fraud engine (see usp_Property_Update), so a Flagged or
/// Rejected listing the seller corrects goes straight back into the
/// review pipeline.
/// </summary>
public class UpdatePropertyRequest
{
    public string? Title { get; set; }

    public string? Description { get; set; }

    public string? Location { get; set; }

    public string? District { get; set; }

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public double? Size { get; set; }

    public decimal? Price { get; set; }

    public string? DeedReference { get; set; }

    /// <summary>The deed's registered owner name. Optional here (ISNULL-coalesced like every other field) - omitting it on an edit leaves the existing value unchanged, it does not clear a mandatory field.</summary>
    public string? OwnerName { get; set; }

    /// <summary>The deed's registered owner NIC. Optional here - see OwnerName's doc comment.</summary>
    public string? OwnerNic { get; set; }

    /// <summary>The deed's registered owner address. Optional here - see OwnerName's doc comment.</summary>
    public string? OwnerAddress { get; set; }

    /// <summary>
    /// True to re-geocode Latitude/Longitude from the (possibly just-changed)
    /// Location/District instead of keeping whatever coordinates were set
    /// before - ignored if Latitude/Longitude are supplied explicitly.
    /// </summary>
    public bool RegeocodeLocation { get; set; }
}
