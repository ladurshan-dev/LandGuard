namespace LandGuard.Application.Common.Interfaces;

/// <summary>
/// Abstraction over password hashing so Application code (AuthService)
/// never references a specific hashing library directly. Implemented in
/// Infrastructure with BCrypt, matching the algorithm the seed data's
/// password hashes already use (Module 2's <c>05_SeedData.sql</c> - every
/// seeded account's hash is BCrypt of <c>Test@123</c>), so seeded test
/// accounts and freshly registered accounts are verifiable the same way.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>Hashes a plaintext password. Never store the plaintext, only this result.</summary>
    string Hash(string password);

    /// <summary>Verifies a plaintext password against a previously produced hash.</summary>
    bool Verify(string password, string passwordHash);
}
