using LandGuard.Application.Common.Models;

namespace LandGuard.Application.Common.Interfaces.StoredProcedures;

/// <summary>
/// Application-layer contract over LandGuardDB's notification stored
/// procedures (<c>usp_Notification_GetByUser</c>,
/// <c>usp_Notification_MarkRead</c>). Implemented in Infrastructure using
/// Dapper (see <c>NotificationStoredProcedures</c>) - Application only
/// ever sees this interface and plain DTOs, never a SQL string or a
/// Dapper type.
///
/// This is the first of several I*StoredProcedures interfaces the project
/// will grow one module at a time - <c>IPropertyStoredProcedures</c>,
/// <c>IFraudStoredProcedures</c>, <c>IAdminStoredProcedures</c>,
/// <c>IUserStoredProcedures</c>, <c>IBuyerFeatureStoredProcedures</c> and
/// <c>IPodcastStoredProcedures</c> will each land alongside their
/// respective feature module, following exactly this same shape.
/// </summary>
public interface INotificationStoredProcedures
{
    /// <summary>Wraps usp_Notification_GetByUser. Newest first, unread-first ordering is handled by the procedure.</summary>
    Task<IReadOnlyList<NotificationSummary>> GetByUserAsync(
        int userId, bool unreadOnly = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Wraps usp_Notification_MarkRead. Pass null for
    /// <paramref name="notificationId"/> to mark every notification for
    /// the user as read. Returns the number of rows updated.
    /// </summary>
    Task<int> MarkReadAsync(
        int userId, int? notificationId = null, CancellationToken cancellationToken = default);
}
