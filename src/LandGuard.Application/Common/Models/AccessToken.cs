namespace LandGuard.Application.Common.Models;

/// <summary>Result of <see cref="LandGuard.Application.Common.Interfaces.IJwtTokenGenerator"/> - the signed token string plus its expiry, so callers never need to re-derive or guess when it expires.</summary>
public record AccessToken(string Token, DateTime ExpiresAtUtc);
