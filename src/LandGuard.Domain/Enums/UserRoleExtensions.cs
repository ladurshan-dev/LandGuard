namespace LandGuard.Domain.Enums;

/// <summary>
/// Single source of truth for converting between <see cref="UserRole"/>
/// and the literal strings <c>dbo.Users.Role</c> actually stores
/// (<c>'Buyer'</c> / <c>'Seller'</c> / <c>'Admin'</c>, enforced by
/// <c>CK_Users_Role</c>).
///
/// Module 3 note: before this existed, the <c>Administrator</c> &lt;-&gt;
/// <c>"Admin"</c> mapping was written once, inline, inside
/// <c>UserConfiguration</c>'s EF Core value converter. Module 3 needs the
/// exact same mapping in two more places - the JWT role claim written at
/// login, and the <c>@Role</c> parameter passed to
/// <c>usp_User_Register</c> - so it was pulled out here rather than
/// copied a second and third time. <c>UserConfiguration</c> now calls
/// this too, so there is exactly one place that knows the database's
/// spelling of "Administrator".
/// </summary>
public static class UserRoleExtensions
{
    public static string ToDbValue(this UserRole role) => role switch
    {
        UserRole.Buyer => "Buyer",
        UserRole.Seller => "Seller",
        UserRole.Administrator => "Admin",
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown UserRole.")
    };

    public static UserRole FromDbValue(string value) => value switch
    {
        "Buyer" => UserRole.Buyer,
        "Seller" => UserRole.Seller,
        "Admin" => UserRole.Administrator,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unrecognized Users.Role value from the database.")
    };
}
