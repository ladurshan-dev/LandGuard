using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LandGuard.Application.Common.Interfaces;
using LandGuard.Application.Common.Models;
using LandGuard.Domain.Enums;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace LandGuard.Infrastructure.Services;

/// <inheritdoc cref="IJwtTokenGenerator" />
public class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly JwtSettings _settings;
    private readonly IDateTimeService _dateTimeService;

    public JwtTokenGenerator(IOptions<JwtSettings> options, IDateTimeService dateTimeService)
    {
        _settings = options.Value;
        _dateTimeService = dateTimeService;
    }

    public AccessToken GenerateToken(int userId, string email, string name, UserRole role)
    {
        var issuedAt = _dateTimeService.UtcNow;
        var expiresAt = issuedAt.AddMinutes(_settings.AccessTokenExpiryMinutes);

        // These three claim types are exactly what CurrentUserService
        // (Infrastructure/Services/CurrentUserService.cs, Module 1) reads
        // back on every authenticated request via
        // ClaimTypes.NameIdentifier / ClaimTypes.Email / ClaimTypes.Role -
        // that contract was written before Auth existed, and this is
        // where it finally gets fulfilled.
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString(CultureInfo.InvariantCulture)),
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.Name, name),
            new Claim(ClaimTypes.Role, role.ToDbValue()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key));
        var signingCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: issuedAt,
            expires: expiresAt,
            signingCredentials: signingCredentials);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        return new AccessToken(tokenString, expiresAt);
    }
}
