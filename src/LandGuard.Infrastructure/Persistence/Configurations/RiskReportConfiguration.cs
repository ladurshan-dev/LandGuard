using LandGuard.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LandGuard.Infrastructure.Persistence.Configurations;

public class RiskReportConfiguration : IEntityTypeConfiguration<RiskReport>
{
    public void Configure(EntityTypeBuilder<RiskReport> builder)
    {
        builder.ToTable("RiskReport", "dbo");

        builder.HasKey(r => r.ReportId);
        builder.Property(r => r.ReportId).HasColumnName("ReportID").ValueGeneratedOnAdd();

        builder.Property(r => r.FraudCheckId).HasColumnName("FraudCheckID").IsRequired();
        builder.Property(r => r.RiskScore).HasColumnName("RiskScore").IsRequired();
        builder.Property(r => r.Summary).HasColumnName("Summary").HasColumnType("nvarchar(max)");
        builder.Property(r => r.GeneratedDate).HasColumnName("GeneratedDate").HasColumnType("datetime2(0)").IsRequired();

        builder.Property(r => r.RiskLevel)
            .HasColumnName("RiskLevel")
            .HasColumnType("varchar(20)")
            .HasConversion<string>()
            .IsRequired();

        // UQ_RiskReport_FraudCheck - this is what makes the relationship 1:1.
        builder.HasIndex(r => r.FraudCheckId).IsUnique().HasDatabaseName("UQ_RiskReport_FraudCheck");

        builder.HasOne(r => r.FraudCheck)
            .WithOne(f => f.RiskReport)
            .HasForeignKey<RiskReport>(r => r.FraudCheckId)
            .OnDelete(DeleteBehavior.Cascade); // FK_RiskReport_FraudCheck: CASCADE
    }
}
