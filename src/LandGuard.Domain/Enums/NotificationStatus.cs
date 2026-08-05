namespace LandGuard.Domain.Enums;

/// <summary>
/// Read state of a <c>dbo.Notification</c> row (<c>CK_Notification_Status</c>).
/// Drives the unread-count badge on the notification bell for every role
/// (Buyer/Seller/Admin all receive notifications from the same table).
/// </summary>
public enum NotificationStatus
{
    Unread = 1,
    Read = 2
}
