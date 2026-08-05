namespace LandGuard.Domain.Enums;

/// <summary>
/// The three roles LandGuard recognizes. Authorization policies and
/// [Authorize(Roles = "...")] attributes will reference these values by
/// name once the Auth module is implemented.
///
/// Module 2 note: <c>dbo.Users.Role</c> is <c>VARCHAR(20)</c> and its
/// <c>CK_Users_Role</c> constraint only accepts the literal strings
/// <c>'Buyer'</c>, <c>'Seller'</c>, <c>'Admin'</c> - not <c>'Administrator'</c>.
/// The C# member below is kept as <see cref="Administrator"/> because it
/// reads better and matches the product's own role vocabulary; a custom
/// EF Core value converter in <c>UserConfiguration</c> is what translates
/// <see cref="Administrator"/> &lt;-&gt; <c>"Admin"</c> at the database
/// boundary, so no other code needs to know about the spelling
/// difference.
/// </summary>
public enum UserRole
{
    Buyer = 1,
    Seller = 2,
    Administrator = 3
}
