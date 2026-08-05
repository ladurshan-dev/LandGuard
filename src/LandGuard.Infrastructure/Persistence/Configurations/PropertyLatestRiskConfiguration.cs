using LandGuard.Domain.ReadModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LandGuard.Infrastructure.Persistence.Configurations;

public class PropertyLatestRiskConfiguration : IEntityTypeConfiguration<PropertyLatestRisk>
{
    public void Configure(EntityTypeBuilder<PropertyLatestRisk> builder)
    {
        // Keyless: this is a read-only projection over FraudCheck + RiskReport,
        // not an updatable table, so EF Core must never try to track identity
        // or generate INSERT/UPDATE/DELETE statements for it.
        builder.HasNoKey();
        builder.ToView("vw_PropertyLatestRisk", "dbo");

        builder.Property(r => r.PropertyId).HasColumnName("PropertyID");
        builder.Property(r => r.FraudCheckId).HasColumnName("FraudCheckID");
        builder.Property(r => r.NicCheck).HasColumnName("NICCheck");
        builder.Property(r => r.FraudStatus).HasColumnType("varchar(20)");
        builder.Property(r => r.CheckDate).HasColumnType("datetime2(0)");
        builder.Property(r => r.ReportId).HasColumnName("ReportID");
        builder.Property(r => r.RiskLevel).HasColumnType("varchar(20)");
        builder.Property(r => r.Summary).HasColumnType("nvarchar(max)");
        builder.Property(r => r.GeneratedDate).HasColumnType("datetime2(0)");
    }
}
