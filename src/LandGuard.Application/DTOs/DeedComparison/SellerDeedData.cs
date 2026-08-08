namespace LandGuard.Application.DTOs.DeedComparison;

/// <summary>
/// Normalized seller-side deed fields (Government Registry module, Phase
/// 4), compared field-by-field against <see cref="GovernmentDeedData"/> by
/// <c>Services.DeedFieldComparer</c>. Deliberately never bound directly
/// from an HTTP request body: every property here is populated by
/// <c>GovernmentDeedComparisonService</c> either from OCR'ing the seller's
/// actually-uploaded deed PDF (via the existing
/// <c>IOcrDocumentService</c>/<c>DocumentFieldExtractor</c> pipeline) or
/// from a property field LandGuard already legitimately captured at
/// listing time (see <see cref="AskingPrice"/>) - never from a client-
/// supplied JSON object claiming to be "the contents of the deed". A
/// seller cannot cause a favourable comparison result simply by typing
/// trusted-looking values into a request.
/// </summary>
public class SellerDeedData
{
    public string? Nic { get; set; }

    public string? OwnerName { get; set; }

    /// <summary>Mapped from the OCR'd deed's "RegistrationNumber" field (see DocumentFieldExtractor - that field's labels already include "Deed Number"/"Deed No").</summary>
    public string? DeedNumber { get; set; }

    public string? PropertyReference { get; set; }

    /// <summary>Perches - mapped from the OCR'd deed's "LandExtent" field.</summary>
    public double? LandSize { get; set; }

    public string? District { get; set; }

    /// <summary>Mapped from the OCR'd deed's "PropertyAddress" field.</summary>
    public string? Address { get; set; }

    /// <summary>
    /// The seller's own current asking price for this listing
    /// (<c>Property.Price</c>, already captured at listing creation/edit
    /// time) - deliberately NOT an OCR-extracted value from the deed PDF
    /// itself. This is a different business concept from
    /// <see cref="GovernmentDeedData.RegisteredPrice"/> (the price
    /// recorded at a past registration/transfer): comparing them checks
    /// for a gross anomaly between "what it's listed for now" and "what
    /// the government's records show it was worth when registered," not
    /// equality between two versions of the same number. See
    /// <c>DeedFieldComparer</c>'s price-comparison logic.
    /// </summary>
    public decimal? AskingPrice { get; set; }

    /// <summary>Mapped from the OCR'd deed's pattern-matched "Date" field - a best-effort, first-date-found heuristic (see DocumentFieldExtractor's own doc comment); not guaranteed to be the deed's registration date specifically if the document contains more than one date.</summary>
    public DateTime? RegistrationDate { get; set; }
}
