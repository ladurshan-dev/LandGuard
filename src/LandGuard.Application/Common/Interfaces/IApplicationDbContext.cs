using LandGuard.Domain.Entities;
using LandGuard.Domain.ReadModels;
using Microsoft.EntityFrameworkCore;

namespace LandGuard.Application.Common.Interfaces;

/// <summary>
/// Abstraction over the EF Core DbContext. Application-layer services
/// depend on this interface, not on LandGuard.Infrastructure's concrete
/// ApplicationDbContext - this is what lets persistence stay swappable
/// and lets services be unit tested against an in-memory implementation
/// (Dependency Inversion Principle).
///
/// Module 2 note: this interface does take a package reference to
/// Microsoft.EntityFrameworkCore, purely for the <c>DbSet&lt;T&gt;</c>
/// generic type used below - there is no lighter-weight package that
/// defines it. Application still writes no provider-specific code (no
/// <c>UseSqlServer</c>, no migrations, no raw SQL); LINQ against these
/// DbSets is translated by whatever provider Infrastructure configures.
///
/// All 12 LandGuardDB tables and all 9 read-only views are exposed here
/// now that Module 2 has mapped the full uploaded schema. Per the
/// project's rules, writing to the 12 table DbSets via
/// <c>SaveChangesAsync</c> is appropriate only for
/// <see cref="PriceBenchmarks"/> (the one table with no stored procedure
/// of its own) - every other table's inserts/updates/deletes must go
/// through the matching stored-procedure wrapper described in
/// LandGuard.Infrastructure.Persistence.StoredProcedures, because the
/// business rules (fraud engine trigger, notifications, audit trail,
/// validation) live in T-SQL, not in this DbContext.
/// </summary>
public interface IApplicationDbContext
{
    // Tables ------------------------------------------------------------
    DbSet<User> Users { get; }
    DbSet<Property> Properties { get; }
    DbSet<PropertyImage> PropertyImages { get; }
    DbSet<FraudCheck> FraudChecks { get; }
    DbSet<RiskReport> RiskReports { get; }
    DbSet<SuspiciousReport> SuspiciousReports { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<Podcast> Podcasts { get; }
    DbSet<SavedProperty> SavedProperties { get; }
    DbSet<AdminAction> AdminActions { get; }

    /// <summary>The one table with no stored procedure - safe to write via SaveChangesAsync.</summary>
    DbSet<PriceBenchmark> PriceBenchmarks { get; }

    DbSet<FraudRuleWeight> FraudRuleWeights { get; }

    // Read-only views -----------------------------------------------------
    DbSet<PropertyLatestRisk> PropertyLatestRisks { get; }
    DbSet<PropertyListing> PropertyListings { get; }
    DbSet<PublishedProperty> PublishedProperties { get; }
    DbSet<FraudCheckDetail> FraudCheckDetails { get; }
    DbSet<FlaggedProperty> FlaggedProperties { get; }
    DbSet<SellerDashboard> SellerDashboards { get; }
    DbSet<BuyerSavedProperty> BuyerSavedProperties { get; }
    DbSet<FraudStatistics> FraudStatistics { get; }
    DbSet<RuleTriggerFrequency> RuleTriggerFrequencies { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
