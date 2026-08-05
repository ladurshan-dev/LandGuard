using LandGuard.Domain.ReadModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LandGuard.Infrastructure.Persistence.Configurations;

public class PublishedPropertyConfiguration : IEntityTypeConfiguration<PublishedProperty>
{
    public void Configure(EntityTypeBuilder<PublishedProperty> builder)
    {
        builder.HasNoKey();
        builder.ToView("vw_PublishedProperty", "dbo");

        builder.Property(p => p.PropertyId).HasColumnName("PropertyID");
        builder.Property(p => p.Latitude).HasColumnType("decimal(9,6)");
        builder.Property(p => p.Longitude).HasColumnType("decimal(9,6)");
        builder.Property(p => p.Price).HasColumnType("decimal(14,2)");
        builder.Property(p => p.PricePerPerch).HasColumnType("decimal(14,2)");
        builder.Property(p => p.Status).HasColumnType("varchar(20)");
        builder.Property(p => p.UploadDate).HasColumnType("datetime2(0)");
        builder.Property(p => p.SellerId).HasColumnName("SellerID");
        builder.Property(p => p.SellerPhone).HasColumnType("varchar(20)");
        builder.Property(p => p.SellerNicVerified).HasColumnName("SellerNICVerified");
        builder.Property(p => p.RiskLevel).HasColumnType("varchar(20)");
        builder.Property(p => p.FraudStatus).HasColumnType("varchar(20)");
        builder.Property(p => p.RiskSummary).HasColumnType("nvarchar(max)");
        builder.Property(p => p.RiskGeneratedDate).HasColumnType("datetime2(0)");
        builder.Property(p => p.CoverImageUrl).HasColumnName("CoverImageURL");
    }
}
