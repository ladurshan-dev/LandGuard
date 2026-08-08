namespace LandGuard.Application.DTOs.GovernmentRegistry;

/// <summary>
/// One trusted record from the (dummy, Phase 1) government land registry -
/// the official counterpart a seller's uploaded deed/listing will
/// eventually be compared against by a later phase's OCR-comparison
/// workflow. Deliberately its own shape, not a second copy of
/// <c>PropertyListingResult</c>/the <c>Property</c> entity: this data
/// represents what the government considers true, while <c>Property</c>
/// represents what the seller submitted, and the entire point of the
/// planned comparison step is to detect where the two disagree. Naming and
/// nullability otherwise follow the same read-DTO convention
/// <c>PropertyListingResult</c>/<c>OcrResultResponse</c> already use in
/// this solution.
/// </summary>
public class GovernmentLandRecordDto
{
    /// <summary>
    /// The government registry's own natural identifier for this record,
    /// e.g. "GR-000001" - a string business key, not a database identity
    /// column, following the same natural-key precedent
    /// <c>FraudRuleWeight.RuleCode</c> already establishes in this
    /// solution (see its doc comment) rather than introducing a GUID.
    /// </summary>
    public string RecordId { get; set; } = null!;

    /// <summary>Sri Lankan NIC of the record's registered owner.</summary>
    public string Nic { get; set; } = null!;

    public string OwnerName { get; set; } = null!;

    /// <summary>Government-assigned parcel/property reference, e.g. "PROP-LK-0001".</summary>
    public string PropertyReference { get; set; } = null!;

    /// <summary>The deed number officially registered for this property, e.g. "DEED-2026-0001".</summary>
    public string DeedNumber { get; set; } = null!;

    public string Address { get; set; } = null!;

    /// <summary>Matches the district naming <c>Property.District</c> uses (fraud rule 1's price-benchmark grouping).</summary>
    public string District { get; set; } = null!;

    /// <summary>Land size in perches, matching <c>Property.Size</c>'s unit.</summary>
    public double LandSize { get; set; }

    /// <summary>Officially registered price in LKR at the time of registration.</summary>
    public decimal RegisteredPrice { get; set; }

    public DateTime RegistrationDate { get; set; }

    /// <summary>"Active" | "Cancelled" | "Suspended" - a Cancelled/Suspended record represents a deed the government no longer recognises as currently valid.</summary>
    public string Status { get; set; } = null!;

    /// <summary>
    /// The <c>StoredDocumentFile.StorageReference</c>
    /// <see cref="Interfaces.IFileStorageService.SaveGovernmentDocumentAsync"/>
    /// returned for this record's trusted government deed PDF (Phase 3),
    /// e.g. "documents/government-registry/GR-000001/&lt;guid&gt;.pdf" -
    /// a logical storage key, deliberately not named "...Path": it is
    /// never an absolute filesystem path and never a public URL (the same
    /// distinction <c>StoredDocumentFile</c>'s own doc comment draws for
    /// the seller-uploaded equivalent). This DTO is returned as-is by
    /// <c>GovernmentRegistryController</c>'s lookup endpoints, so nothing
    /// stored here may ever be a real OS path - only Infrastructure
    /// (<c>LocalFileStorageService</c>) knows how to resolve this
    /// reference back to a physical location. Null when a record has no
    /// government deed document on file.
    /// </summary>
    public string? DeedDocumentReference { get; set; }
}
