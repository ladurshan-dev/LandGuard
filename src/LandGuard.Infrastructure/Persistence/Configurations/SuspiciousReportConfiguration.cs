using LandGuard.Domain.Entities;
using LandGuard.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LandGuard.Infrastructure.Persistence.Configurations;

public class SuspiciousReportConfiguration : IEntityTypeConfiguration<SuspiciousReport>
{
    public void Configure(EntityTypeBuilder<SuspiciousReport> builder)
    {
        builder.ToTable("SuspiciousReport", "dbo");

        builder.HasKey(s => s.SuspiciousReportId);
        builder.Property(s => s.SuspiciousReportId).HasColumnName("SuspiciousReportID").ValueGeneratedOnAdd();

        builder.Property(s => s.BuyerId).HasColumnName("BuyerID").IsRequired();
        builder.Property(s => s.PropertyId).HasColumnName("PropertyID").IsRequired();
        builder.Property(s => s.Reason).HasColumnName("Reason").HasColumnType("nvarchar(255)").IsRequired();
        builder.Property(s => s.Description).HasColumnName("Description").HasColumnType("nvarchar(max)");
        builder.Property(s => s.ReportDate).HasColumnName("ReportDate").HasColumnType("datetime2(0)").IsRequired();

        // dbo.SuspiciousReport.Status stores the two-word literal
        // 'Under Review', which isn't a valid C# enum member name, so this
        // needs a custom converter rather than the default HasConversion<string>().
        builder.Property(s => s.Status)
            .HasColumnName("Status")
            .HasColumnType("varchar(20)")
            .HasConversion(
                status => status == ReportStatus.UnderReview ? "Under Review" : status.ToString(),
                value => value == "Under Review" ? ReportStatus.UnderReview : Enum.Parse<ReportStatus>(value))
            .IsRequired();

        // UQ_SuspiciousReport_Once - same buyer cannot file the same reason twice.
        builder.HasIndex(s => new { s.BuyerId, s.PropertyId, s.Reason })
            .IsUnique()
            .HasDatabaseName("UQ_SuspiciousReport_Once");

        builder.HasOne(s => s.Buyer)
            .WithMany(u => u.SuspiciousReportsFiled)
            .HasForeignKey(s => s.BuyerId)
            .OnDelete(DeleteBehavior.Restrict); // FK_SuspiciousReport_Buyer: NO ACTION

        builder.HasOne(s => s.Property)
            .WithMany(p => p.SuspiciousReports)
            .HasForeignKey(s => s.PropertyId)
            .OnDelete(DeleteBehavior.Cascade); // FK_SuspiciousReport_Property: CASCADE
    }
}
