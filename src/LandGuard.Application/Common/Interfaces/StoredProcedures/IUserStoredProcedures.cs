using LandGuard.Application.Common.Models;
using LandGuard.Domain.Enums;

namespace LandGuard.Application.Common.Interfaces.StoredProcedures;

/// <summary>
/// Application-layer contract over LandGuardDB's user/authentication
/// stored procedures. Implemented in Infrastructure with Dapper (see
/// <c>UserStoredProcedures</c>), following exactly the pattern
/// <c>INotificationStoredProcedures</c>/<c>NotificationStoredProcedures</c>
/// established in Module 2.
///
/// <see cref="ChangePasswordAsync"/> wraps <c>usp_User_ChangePassword</c>,
/// a new, narrowly-scoped procedure added in Module 3 (see
/// <c>database/Module3_ChangePassword.sql</c>) - Module 2's package had no
/// way to update <c>PasswordHash</c> after registration.
/// </summary>
public interface IUserStoredProcedures
{
    /// <summary>
    /// Wraps usp_User_Register. Throws the underlying SqlException (mapped
    /// to a 400 response by ExceptionHandlingMiddleware) if the email is
    /// already registered, the role is invalid, or - for a Seller - the
    /// NIC is missing/invalid/already linked to another account.
    /// </summary>
    Task<UserProfile> RegisterAsync(
        string name,
        string email,
        string passwordHash,
        UserRole role,
        string? nic,
        string? phone,
        CancellationToken cancellationToken = default);

    /// <summary>Wraps usp_User_Login. Includes PasswordHash - see UserCredential's doc comment. Null if no user has that email.</summary>
    Task<UserCredential?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>Wraps usp_User_GetById. Null if the id doesn't exist.</summary>
    Task<UserProfile?> GetByIdAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>Wraps usp_User_ChangePassword (Module 3). Returns the number of rows updated (0 or 1).</summary>
    Task<int> ChangePasswordAsync(int userId, string newPasswordHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Wraps usp_User_SetIdentityStatus (Seller Government Identity
    /// Verification requirement) - the only write path for
    /// dbo.Users.IdentityStatus. Called exclusively by
    /// SellerIdentityVerificationService. Throws (SqlException, RAISERROR)
    /// for a non-Seller userId - see that procedure's own header comment.
    /// </summary>
    /// <param name="identityStatus">LandGuard.Domain.Enums.IdentityStatus's exact string name - "Pending" | "Verified" | "Failed".</param>
    Task SetIdentityStatusAsync(int userId, string identityStatus, CancellationToken cancellationToken = default);
}
