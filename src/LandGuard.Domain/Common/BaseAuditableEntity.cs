namespace LandGuard.Domain.Common;

/// <summary>
/// Adds "who/when" audit metadata on top of BaseEntity. Fraud investigation
/// is core to LandGuard - when an Admin disputes a rejection, or a Buyer
/// reports a listing as fraudulent, being able to answer "who created this
/// row and when was it last touched" is not optional, it's part of the
/// product's integrity story. Entities that participate in the fraud
/// workflow (Property, PropertyDocument, FraudReport, ...) should inherit
/// from this instead of BaseEntity directly. The fields are populated
/// automatically by AuditableEntitySaveChangesInterceptor in the
/// Infrastructure layer, so no service is responsible for setting them
/// by hand.
/// </summary>
public abstract class BaseAuditableEntity : BaseEntity
{
    public DateTime CreatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public DateTime? LastModifiedAt { get; set; }

    public string? LastModifiedBy { get; set; }
}
