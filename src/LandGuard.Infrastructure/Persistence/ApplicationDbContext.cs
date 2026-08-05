using LandGuard.Application.Common.Interfaces;
using LandGuard.Domain.Entities;
using LandGuard.Domain.ReadModels;
using Microsoft.EntityFrameworkCore;

namespace LandGuard.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of IApplicationDbContext - the single seam
/// between the Domain/Application layers and the pre-existing LandGuardDB
/// database uploaded and analysed in Module 2.
///
/// This context does not manage schema. LandGuardDB is created and owned
/// by <c>Database/Scripts/00_RunAll.sql</c> (schema, indexes, views,
/// stored procedures, seed data) - EF Core Migrations are deliberately
/// never used against it. The Fluent configuration below only *describes*
/// what already exists, so `dotnet ef migrations add` should never be run
/// for this DbContext; if the model and the live database ever disagree,
/// the SQL scripts are the source of truth, not this file.
///
/// Entity configuration (table/column names, precision, enum converters,
/// FK delete behavior) is done via IEntityTypeConfiguration&lt;T&gt;
/// classes under Persistence/Configurations, picked up automatically by
/// ApplyConfigurationsFromAssembly - one file per table/view keeps mapping
/// concerns out of the entities themselves (which stay POCOs) and out of
/// a single sprawling OnModelCreating method.
/// </summary>
public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // Tables ------------------------------------------------------------
    public DbSet<User> Users => Set<User>();
    public DbSet<Property> Properties => Set<Property>();
    public DbSet<PropertyImage> PropertyImages => Set<PropertyImage>();
    public DbSet<FraudCheck> FraudChecks => Set<FraudCheck>();
    public DbSet<RiskReport> RiskReports => Set<RiskReport>();
    public DbSet<SuspiciousReport> SuspiciousReports => Set<SuspiciousReport>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Podcast> Podcasts => Set<Podcast>();
    public DbSet<SavedProperty> SavedProperties => Set<SavedProperty>();
    public DbSet<AdminAction> AdminActions => Set<AdminAction>();
    public DbSet<PriceBenchmark> PriceBenchmarks => Set<PriceBenchmark>();
    public DbSet<FraudRuleWeight> FraudRuleWeights => Set<FraudRuleWeight>();

    // Read-only views -----------------------------------------------------
    public DbSet<PropertyLatestRisk> PropertyLatestRisks => Set<PropertyLatestRisk>();
    public DbSet<PropertyListing> PropertyListings => Set<PropertyListing>();
    public DbSet<PublishedProperty> PublishedProperties => Set<PublishedProperty>();
    public DbSet<FraudCheckDetail> FraudCheckDetails => Set<FraudCheckDetail>();
    public DbSet<FlaggedProperty> FlaggedProperties => Set<FlaggedProperty>();
    public DbSet<SellerDashboard> SellerDashboards => Set<SellerDashboard>();
    public DbSet<BuyerSavedProperty> BuyerSavedProperties => Set<BuyerSavedProperty>();
    public DbSet<FraudStatistics> FraudStatistics => Set<FraudStatistics>();
    public DbSet<RuleTriggerFrequency> RuleTriggerFrequencies => Set<RuleTriggerFrequency>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // Scalar UDF mappings - lets these three functions be called both
        // directly (ctx.IsValidNic(nic)) and inside a LINQ query, where EF
        // Core translates the call to dbo.fn_X(...) in the generated SQL
        // instead of pulling data client-side to evaluate it in C#.
        modelBuilder.HasDbFunction(typeof(ApplicationDbContext)
                .GetMethod(nameof(IsValidNic), new[] { typeof(string) })!)
            .HasName("fn_IsValidNIC")
            .HasSchema("dbo");

        modelBuilder.HasDbFunction(typeof(ApplicationDbContext)
                .GetMethod(nameof(RiskLevelFromScore), new[] { typeof(int) })!)
            .HasName("fn_RiskLevelFromScore")
            .HasSchema("dbo");

        modelBuilder.HasDbFunction(typeof(ApplicationDbContext)
                .GetMethod(nameof(GetRuleWeight), new[] { typeof(string) })!)
            .HasName("fn_GetRuleWeight")
            .HasSchema("dbo");

        base.OnModelCreating(modelBuilder);
    }

    /// <summary>Maps to dbo.fn_IsValidNIC. Client-side use throws - call only inside a LINQ query translated to SQL.</summary>
    public static bool IsValidNic(string? nic) =>
        throw new NotSupportedException($"{nameof(IsValidNic)} is for use inside a LINQ query only; it translates to dbo.fn_IsValidNIC.");

    /// <summary>Maps to dbo.fn_RiskLevelFromScore. Client-side use throws - call only inside a LINQ query translated to SQL.</summary>
    public static string RiskLevelFromScore(int score) =>
        throw new NotSupportedException($"{nameof(RiskLevelFromScore)} is for use inside a LINQ query only; it translates to dbo.fn_RiskLevelFromScore.");

    /// <summary>Maps to dbo.fn_GetRuleWeight. Client-side use throws - call only inside a LINQ query translated to SQL.</summary>
    public static int GetRuleWeight(string ruleCode) =>
        throw new NotSupportedException($"{nameof(GetRuleWeight)} is for use inside a LINQ query only; it translates to dbo.fn_GetRuleWeight.");
}
