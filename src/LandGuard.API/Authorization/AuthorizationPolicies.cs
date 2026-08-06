namespace LandGuard.API.Authorization;

/// <summary>
/// Named authorization policy constants, registered against
/// <c>AddAuthorizationBuilder()</c> in Program.cs and referenced from
/// controllers via <c>[Authorize(Policy = AuthorizationPolicies.RequireX)]</c>.
/// Named policies (rather than raw <c>[Authorize(Roles = "Admin")]</c>
/// strings scattered across controllers) are the .NET 8 idiom and keep the
/// three role names in exactly one place - the role claim value itself is
/// "Admin" (not "Administrator"; see <c>UserRoleExtensions.ToDbValue</c>),
/// so every policy definition here is the single place that spelling
/// needs to be correct.
/// </summary>
public static class AuthorizationPolicies
{
    public const string RequireBuyer = "RequireBuyer";

    public const string RequireSeller = "RequireSeller";

    public const string RequireAdmin = "RequireAdmin";

    public const string RequireSellerOrAdmin = "RequireSellerOrAdmin";

    public const string BuyerRole = "Buyer";

    public const string SellerRole = "Seller";

    public const string AdminRole = "Admin";
}
