namespace LandGuard.Domain.Entities;

/// <summary>
/// Maps to <c>dbo.PriceBenchmark</c> [ext] - the reference market rate per
/// perch, by district, that fraud rule 1 (Price Anomaly) compares against.
/// The link to <see cref="Entities.Property.District"/> is a same-named
/// string match evaluated at query time inside
/// <c>usp_Fraud_AnalyseProperty</c>, not a physical foreign key (see the
/// ERD's dotted, non-identifying relationship line) - so there is no
/// navigation property here.
///
/// This is the one table in the schema with <b>no</b> stored procedure of
/// its own, so unlike every other entity in this module, direct EF Core
/// CRUD (<c>DbSet.Add</c>/<c>Update</c>/<c>Remove</c> + <c>SaveChanges</c>)
/// is the correct way to maintain it.
/// </summary>
public class PriceBenchmark
{
    public int BenchmarkId { get; set; }

    public string District { get; set; } = null!;

    public decimal MarketPricePerPerch { get; set; }

    public DateTime UpdatedDate { get; set; }
}
