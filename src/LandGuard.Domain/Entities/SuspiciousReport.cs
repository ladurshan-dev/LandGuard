using LandGuard.Domain.Enums;

namespace LandGuard.Domain.Entities;

/// <summary>
/// Maps to <c>dbo.SuspiciousReport</c> - a Buyer reporting a listing as
/// fraudulent (FR12). <c>UQ_SuspiciousReport_Once</c> stops the same buyer
/// filing the same reason on the same property twice. Insert goes through
/// <c>usp_SuspiciousReport_Create</c>; resolution goes through
/// <c>usp_Admin_ResolveReport</c>.
/// </summary>
public class SuspiciousReport
{
    public int SuspiciousReportId { get; set; }

    public int BuyerId { get; set; }

    public int PropertyId { get; set; }

    public string Reason { get; set; } = null!;

    public string? Description { get; set; }

    public DateTime ReportDate { get; set; }

    public ReportStatus Status { get; set; }

    // Navigation properties -------------------------------------------------

    public User Buyer { get; set; } = null!;

    public Property Property { get; set; } = null!;

    /// <summary>Admin actions taken against this report (usp_Admin_ResolveReport).</summary>
    public ICollection<AdminAction> AdminActions { get; set; } = new List<AdminAction>();
}
