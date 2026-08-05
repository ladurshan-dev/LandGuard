using LandGuard.Application.Common.Interfaces.StoredProcedures;
using LandGuard.Application.Common.Models;

namespace LandGuard.Infrastructure.Persistence.StoredProcedures;

/// <summary>
/// Infrastructure implementation of <see cref="INotificationStoredProcedures"/>.
/// The proven pattern for every future *StoredProcedures class: inject
/// <see cref="IStoredProcedureExecutor"/>, pass exact stored-procedure
/// parameter names (matching the <c>@ParamName</c> the T-SQL declares) as
/// an anonymous object, and map the result straight into a plain DTO from
/// LandGuard.Application.Common.Models. No business logic lives here -
/// validation, notifications and audit trail are already handled inside
/// the stored procedures themselves.
/// </summary>
public class NotificationStoredProcedures : INotificationStoredProcedures
{
    private readonly IStoredProcedureExecutor _executor;

    public NotificationStoredProcedures(IStoredProcedureExecutor executor)
    {
        _executor = executor;
    }

    public Task<IReadOnlyList<NotificationSummary>> GetByUserAsync(
        int userId, bool unreadOnly = false, CancellationToken cancellationToken = default)
    {
        var parameters = new { UserID = userId, UnreadOnly = unreadOnly };

        return _executor.QueryAsync<NotificationSummary>(
            "dbo.usp_Notification_GetByUser", parameters, cancellationToken);
    }

    public async Task<int> MarkReadAsync(
        int userId, int? notificationId = null, CancellationToken cancellationToken = default)
    {
        var parameters = new { UserID = userId, NotificationID = notificationId };

        // usp_Notification_MarkRead returns a single row with one column,
        // RowsUpdated - QuerySingleOrDefaultAsync<int> maps it directly.
        var rowsUpdated = await _executor.QuerySingleOrDefaultAsync<int>(
            "dbo.usp_Notification_MarkRead", parameters, cancellationToken);

        return rowsUpdated;
    }
}
