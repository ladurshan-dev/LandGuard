namespace LandGuard.Application.Common.Models;

/// <summary>
/// Shape returned by <c>dbo.usp_Notification_GetByUser</c>
/// (NotificationID, Message, NotificationDate, Status, RelatedPropertyID).
/// A dedicated read DTO rather than the Domain <c>Notification</c> entity
/// because the procedure doesn't return <c>UserID</c> (the caller already
/// knows it - it's the parameter) and this type only ever comes from a
/// Dapper projection, never from a tracked EF Core query.
/// </summary>
public class NotificationSummary
{
    public int NotificationId { get; set; }

    public string Message { get; set; } = null!;

    public DateTime NotificationDate { get; set; }

    /// <summary>"Read" | "Unread".</summary>
    public string Status { get; set; } = null!;

    public int? RelatedPropertyId { get; set; }
}
