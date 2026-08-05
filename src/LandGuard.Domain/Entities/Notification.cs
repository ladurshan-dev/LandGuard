using LandGuard.Domain.Enums;

namespace LandGuard.Domain.Entities;

/// <summary>
/// Maps to <c>dbo.Notification</c> (FR07). Every stored procedure that
/// changes a listing's fate or a user's status also inserts one of these -
/// welcome messages, fraud analysis results, admin approvals/rejections,
/// suspensions, report resolutions. Read via
/// <c>usp_Notification_GetByUser</c>, the first stored-procedure wrapper
/// built in Module 2 (see <c>NotificationStoredProcedures</c>).
/// </summary>
public class Notification
{
    public int NotificationId { get; set; }

    public int UserId { get; set; }

    public string Message { get; set; } = null!;

    public DateTime NotificationDate { get; set; }

    public NotificationStatus Status { get; set; }

    /// <summary>[ext] Deep-link target - null for notifications not about a specific listing.</summary>
    public int? RelatedPropertyId { get; set; }

    // Navigation properties -------------------------------------------------

    public User User { get; set; } = null!;

    public Property? RelatedProperty { get; set; }
}
