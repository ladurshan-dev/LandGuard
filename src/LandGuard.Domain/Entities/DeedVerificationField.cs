namespace LandGuard.Domain.Entities;

/// <summary>
/// Maps to <c>dbo.DeedVerificationField</c> (Government Registry module,
/// Phase 5B) - one row per compared field, persisting exactly what
/// <c>DeedFieldComparisonResult</c> already computed
/// (FieldName/GovernmentValue/SellerValue/Match/Message), normalized rather
/// than JSON - matching this schema's existing preference for child rows
/// (<see cref="FraudCheck"/>/<see cref="RiskReport"/>,
/// <see cref="Property"/>/<see cref="PropertyImage"/>,
/// <see cref="SuspiciousReport"/>/<see cref="AdminAction"/>) over blob
/// columns.
///
/// Written exclusively by <c>usp_DeedVerificationField_Add</c>, one call
/// per field, inside the same transaction as the parent
/// <c>usp_DeedVerification_Create</c> insert (see
/// <c>GovernmentDeedVerificationService</c>). No update/delete procedure -
/// see <see cref="DeedVerification"/>'s own doc comment.
/// </summary>
public class DeedVerificationField
{
    public int DeedVerificationFieldId { get; set; }

    public int DeedVerificationId { get; set; }

    /// <summary>e.g. "NIC", "DeedNumber", "LandSize", "Price" - matches <c>DeedFieldComparisonResult.FieldName</c> exactly.</summary>
    public string FieldName { get; set; } = null!;

    public string? GovernmentValue { get; set; }

    public string? SellerValue { get; set; }

    public bool IsMatch { get; set; }

    public string? Message { get; set; }

    // Navigation properties -------------------------------------------------

    public DeedVerification DeedVerification { get; set; } = null!;
}
