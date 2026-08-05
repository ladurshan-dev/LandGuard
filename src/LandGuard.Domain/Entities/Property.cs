using LandGuard.Domain.Enums;

namespace LandGuard.Domain.Entities;

/// <summary>
/// Maps to <c>dbo.Property</c>. Writes go through
/// <c>usp_Property_Create</c> / <c>usp_Property_Update</c> /
/// <c>usp_Property_Delete</c> - creation and every status-changing update
/// immediately triggers <c>usp_Fraud_AnalyseProperty</c>, which no EF Core
/// <c>SaveChanges</c> call can replicate.
/// </summary>
public class Property
{
    public int PropertyId { get; set; }

    public int SellerId { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    /// <summary>Free-text location exactly as typed by the seller.</summary>
    public string Location { get; set; } = null!;

    /// <summary>[ext] Normalised district, used for price benchmarking (fraud rule 1).</summary>
    public string? District { get; set; }

    /// <summary>[ext] Written back from the Nominatim API by the property module (fraud rule 6).</summary>
    public decimal? Latitude { get; set; }

    /// <summary>[ext] Written back from the Nominatim API by the property module (fraud rule 6).</summary>
    public decimal? Longitude { get; set; }

    /// <summary>Land size in perches.</summary>
    public double Size { get; set; }

    /// <summary>Asking price in LKR.</summary>
    public decimal Price { get; set; }

    /// <summary>
    /// [ext] PERSISTED computed column: Price / Size. Never written by EF
    /// Core - SQL Server computes and stores it on every insert/update.
    /// </summary>
    public decimal? PricePerPerch { get; set; }

    public string? DeedReference { get; set; }

    public PropertyStatus Status { get; set; }

    public DateTime UploadDate { get; set; }

    // Navigation properties -------------------------------------------------

    public User Seller { get; set; } = null!;

    public ICollection<PropertyImage> Images { get; set; } = new List<PropertyImage>();

    /// <summary>All analysis runs for this listing (a seller can correct and resubmit). Latest = current.</summary>
    public ICollection<FraudCheck> FraudChecks { get; set; } = new List<FraudCheck>();

    public ICollection<SuspiciousReport> SuspiciousReports { get; set; } = new List<SuspiciousReport>();

    public ICollection<SavedProperty> SavedByBuyers { get; set; } = new List<SavedProperty>();

    public ICollection<AdminAction> AdminActions { get; set; } = new List<AdminAction>();

    /// <summary>Notifications that deep-link back to this property.</summary>
    public ICollection<Notification> RelatedNotifications { get; set; } = new List<Notification>();
}
