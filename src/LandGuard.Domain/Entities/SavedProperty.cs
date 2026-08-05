namespace LandGuard.Domain.Entities;

/// <summary>
/// Maps to <c>dbo.SavedProperty</c> [ext] - the buyer dashboard's "saved
/// listings" feature (FR07). <c>UQ_SavedProperty_Pair</c> prevents saving
/// the same listing twice. Writes go through <c>usp_SavedProperty_Add</c> /
/// <c>usp_SavedProperty_Remove</c>.
/// </summary>
public class SavedProperty
{
    public int SavedPropertyId { get; set; }

    public int BuyerId { get; set; }

    public int PropertyId { get; set; }

    public DateTime SavedDate { get; set; }

    public User Buyer { get; set; } = null!;

    public Property Property { get; set; } = null!;
}
