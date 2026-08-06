using LandGuard.Application.Common.Interfaces;

namespace LandGuard.Infrastructure.Services;

/// <inheritdoc cref="IPasswordHasher" />
public class BcryptPasswordHasher : IPasswordHasher
{
    // Matches the work factor already baked into every seeded password
    // hash (Module 2's 05_SeedData.sql: every $2a$11$... hash is BCrypt of
    // "Test@123" at work factor 11) - freshly registered accounts and
    // seeded test accounts are then hashed the same way, not just
    // verifiable the same way. BCrypt.Verify itself is work-factor
    // agnostic (the factor is embedded in the stored hash), so this
    // constant only controls hashes this class produces going forward.
    private const int WorkFactor = 11;

    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, workFactor: WorkFactor);

    public bool Verify(string password, string passwordHash) => BCrypt.Net.BCrypt.Verify(password, passwordHash);
}
