namespace LandGuard.Application.DTOs.DeedComparison;

/// <summary>
/// Normalized government-side deed fields (Government Registry module,
/// Phase 4), compared field-by-field against <see cref="SellerDeedData"/>
/// by <c>Services.DeedFieldComparer</c>. Every property except
/// <see cref="Status"/> is populated by OCR'ing the trusted government
/// deed PDF (opened via <c>IFileStorageService.OpenDocumentAsync</c> and
/// read with the same <c>IOcrService</c>/<c>DocumentFieldExtractor</c>
/// pipeline used for the seller's own deed) - not read directly off
/// <c>GovernmentLandRecordDto</c>, so the comparison also implicitly
/// verifies the stored PDF's own printed content agrees with the
/// registry's structured record, not just that a PDF exists.
/// <see cref="Status"/> is the one exception: it comes directly from
/// <c>GovernmentLandRecordDto.Status</c>, because that structured value is
/// what gates whether this type is ever built at all - a Cancelled or
/// missing record short-circuits to Scenario F before any government PDF
/// OCR is attempted (see <c>GovernmentDeedComparisonService</c>).
/// </summary>
public class GovernmentDeedData
{
    public string? Nic { get; set; }

    public string? OwnerName { get; set; }

    public string? DeedNumber { get; set; }

    public string? PropertyReference { get; set; }

    /// <summary>Perches.</summary>
    public double? LandSize { get; set; }

    public string? District { get; set; }

    public string? Address { get; set; }

    /// <summary>The price recorded at registration/transfer - a different business concept from <see cref="SellerDeedData.AskingPrice"/>; see that property's doc comment.</summary>
    public decimal? RegisteredPrice { get; set; }

    public DateTime? RegistrationDate { get; set; }

    /// <summary>"Active" | "Cancelled" | "Suspended" - read directly from <c>GovernmentLandRecordDto.Status</c>, not OCR'd. See this class's doc comment.</summary>
    public string? Status { get; set; }
}
