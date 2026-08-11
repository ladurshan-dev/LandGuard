using System.Data;
using Dapper;
using LandGuard.Application.Common.Interfaces.StoredProcedures;
using LandGuard.Application.Common.Models;
using LandGuard.Domain.Enums;

namespace LandGuard.Infrastructure.Persistence.StoredProcedures;

/// <summary>
/// Infrastructure implementation of <see cref="IUserStoredProcedures"/>,
/// following the same pattern <c>NotificationStoredProcedures</c>
/// established in Module 2.
/// </summary>
public class UserStoredProcedures : IUserStoredProcedures
{
    private readonly IStoredProcedureExecutor _executor;

    public UserStoredProcedures(IStoredProcedureExecutor executor)
    {
        _executor = executor;
    }

    public async Task<UserProfile> RegisterAsync(
        string name,
        string email,
        string passwordHash,
        UserRole role,
        string? nic,
        string? phone,
        CancellationToken cancellationToken = default)
    {
        // usp_User_Register has an OUTPUT parameter (@NewUserID) in
        // addition to its final SELECT. Dapper's DynamicParameters is what
        // makes an output parameter possible while still passing the
        // object straight through IStoredProcedureExecutor's plain
        // `object? parameters` signature - no interface change needed.
        var parameters = new DynamicParameters();
        parameters.Add("@Name", name);
        parameters.Add("@Email", email);
        parameters.Add("@PasswordHash", passwordHash);
        parameters.Add("@Role", role.ToDbValue());
        parameters.Add("@NIC", nic);
        parameters.Add("@Phone", phone);
        parameters.Add("@NewUserID", dbType: DbType.Int32, direction: ParameterDirection.Output);

        // The procedure's final SELECT returns exactly the one row it just
        // inserted (WHERE UserID = @NewUserID), so the row and the output
        // parameter describe the same user - only the row is needed here.
        var profile = await _executor.QuerySingleOrDefaultAsync<UserProfile>(
            "dbo.usp_User_Register", parameters, cancellationToken);

        return profile!;
    }

    public Task<UserCredential?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var parameters = new { Email = email };

        return _executor.QuerySingleOrDefaultAsync<UserCredential>(
            "dbo.usp_User_Login", parameters, cancellationToken);
    }

    public Task<UserProfile?> GetByIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        var parameters = new { UserID = userId };

        return _executor.QuerySingleOrDefaultAsync<UserProfile>(
            "dbo.usp_User_GetById", parameters, cancellationToken);
    }

    public async Task<int> ChangePasswordAsync(int userId, string newPasswordHash, CancellationToken cancellationToken = default)
    {
        var parameters = new { UserID = userId, NewPasswordHash = newPasswordHash };

        // usp_User_ChangePassword (Module 3, database/Module3_ChangePassword.sql)
        // returns a single row with one column, RowsUpdated.
        var rowsUpdated = await _executor.QuerySingleOrDefaultAsync<int>(
            "dbo.usp_User_ChangePassword", parameters, cancellationToken);

        return rowsUpdated;
    }

    public async Task SetIdentityStatusAsync(int userId, string identityStatus, CancellationToken cancellationToken = default)
    {
        var parameters = new { UserID = userId, IdentityStatus = identityStatus };

        // usp_User_SetIdentityStatus's own final SELECT (the updated row) is
        // not needed by any current caller - SellerIdentityVerificationService
        // already has the status it just persisted.
        await _executor.QuerySingleOrDefaultAsync<UserProfile>(
            "dbo.usp_User_SetIdentityStatus", parameters, cancellationToken);
    }
}
