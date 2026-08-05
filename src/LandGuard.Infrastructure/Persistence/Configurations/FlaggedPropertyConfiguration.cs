using LandGuard.Domain.ReadModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LandGuard.Infrastructure.Persistence.Configurations;

public class FlaggedPropertyConfiguration : IEntityTypeConfiguration<FlaggedProperty>
{
    public void Configure(EntityTypeBuilder<FlaggedProperty> builder)
    {
        builder.HasNoKey();
        builder.ToView("vw_FlaggedProperty", "dbo");

        builder.Property(f => f.PropertyId).HasColumnName("PropertyID");
        builder.Property(f => f.Price).HasColumnType("decimal(14,2)");
        builder.Property(f => f.Status).HasColumnType("varchar(20)");
        builder.Property(f => f.UploadDate).HasColumnType("datetime2(0)");
        builder.Property(f => f.SellerId).HasColumnName("SellerID");
        builder.Property(f => f.SellerNicVerified).HasColumnName("SellerNICVerified");
        builder.Property(f => f.RiskLevel).HasColumnType("varchar(20)");
        builder.Property(f => f.FraudStatus).HasColumnType("varchar(20)");
        builder.Property(f => f.RiskSummary).HasColumnType("nvarchar(max)");
    }
}
