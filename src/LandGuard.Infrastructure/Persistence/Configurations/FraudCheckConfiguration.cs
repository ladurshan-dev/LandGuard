using LandGuard.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LandGuard.Infrastructure.Persistence.Configurations;

public class FraudCheckConfiguration : IEntityTypeConfiguration<FraudCheck>
{
    public void Configure(EntityTypeBuilder<FraudCheck> builder)
    {
        builder.ToTable("FraudCheck", "dbo");

        builder.HasKey(f => f.FraudCheckId);
        builder.Property(f => f.FraudCheckId).HasColumnName("FraudCheckID").ValueGeneratedOnAdd();

        builder.Property(f => f.PropertyId).HasColumnName("PropertyID").IsRequired();
        builder.Property(f => f.PriceCheck).HasColumnName("PriceCheck").IsRequired();
        builder.Property(f => f.DuplicateCheck).HasColumnName("DuplicateCheck").IsRequired();
        builder.Property(f => f.NicCheck).HasColumnName("NICCheck").IsRequired();
        builder.Property(f => f.DeedCheck).HasColumnName("DeedCheck").IsRequired();
        builder.Property(f => f.SellerHistoryCheck).HasColumnName("SellerHistoryCheck").IsRequired();
        builder.Property(f => f.LocationCheck).HasColumnName("LocationCheck").IsRequired();
        builder.Property(f => f.MissingInfoCheck).HasColumnName("MissingInfoCheck").IsRequired();
        builder.Property(f => f.CheckDate).HasColumnName("CheckDate").HasColumnType("datetime2(0)").IsRequired();

        builder.Property(f => f.FraudStatus)
            .HasColumnName("FraudStatus")
            .HasColumnType("varchar(20)")
            .HasConversion<string>()
            .IsRequired();

        // Latest-analysis-per-property lookup (vw_PropertyLatestRisk relies on this shape).
        builder.HasIndex(f => new { f.PropertyId, f.CheckDate })
            .HasDatabaseName("IX_FraudCheck_Property_Date");

        builder.HasOne(f => f.Property)
            .WithMany(p => p.FraudChecks)
            .HasForeignKey(f => f.PropertyId)
            .OnDelete(DeleteBehavior.Cascade); // FK_FraudCheck_Property: CASCADE

        // The 1:1 side (RiskReport.FraudCheckId is the FK and carries the
        // UNIQUE constraint) is configured in RiskReportConfiguration.
    }
}
