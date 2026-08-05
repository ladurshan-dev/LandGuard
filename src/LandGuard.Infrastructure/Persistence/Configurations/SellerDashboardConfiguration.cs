using LandGuard.Domain.ReadModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LandGuard.Infrastructure.Persistence.Configurations;

public class SellerDashboardConfiguration : IEntityTypeConfiguration<SellerDashboard>
{
    public void Configure(EntityTypeBuilder<SellerDashboard> builder)
    {
        builder.HasNoKey();
        builder.ToView("vw_SellerDashboard", "dbo");

        builder.Property(s => s.SellerId).HasColumnName("SellerID");
        builder.Property(s => s.NicVerified).HasColumnName("NICVerified");
        builder.Property(s => s.AverageRiskScore).HasColumnType("decimal(5,2)");
    }
}
