namespace LandGuard.Infrastructure.Services;

/// <summary>
/// Strongly-typed binding of the "Jwt" configuration section
/// (appsettings.json). Bound via <c>services.Configure&lt;JwtSettings&gt;</c>
/// in Infrastructure's DependencyInjection so <see cref="JwtTokenGenerator"/>
/// and Program.cs's <c>TokenValidationParameters</c> both read from the
/// exact same source - a signing key and a validation key that could
/// silently drift apart would be a much worse bug than a bit of shared
/// configuration.
/// </summary>
public class JwtSettings
{
    public string Issuer { get; set; } = null!;

    public string Audience { get; set; } = null!;

    public string Key { get; set; } = null!;

    public int AccessTokenExpiryMinutes { get; set; } = 60;
}
