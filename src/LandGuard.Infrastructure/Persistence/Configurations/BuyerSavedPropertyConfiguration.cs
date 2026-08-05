using LandGuard.Domain.ReadModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LandGuard.Infrastructure.Persistence.Configurations;

public class BuyerSavedPropertyConfiguration : IEntityTypeConfiguration<BuyerSavedProperty>
{
    public void Configure(EntityTypeBuilder<BuyerSavedProperty> builder)
    {
        builder.HasNoKey();
        builder.ToView("vw_BuyerSavedProperty", "dbo");

        builder.Property(b => b.SavedPropertyId).HasColumnName("SavedPropertyID");
        builder.Property(b => b.BuyerId).HasColumnName("BuyerID");
        builder.Property(b => b.SavedDate).HasColumnType("datetime2(0)");
        builder.Property(b => b.PropertyId).HasColumnName("PropertyID");
        builder.Property(b => b.Price).HasColumnType("decimal(14,2)");
        builder.Property(b => b.Status).HasColumnType("varchar(20)");
        builder.Property(b => b.RiskLevel).HasColumnType("varchar(20)");
        builder.Property(b => b.CoverImageUrl).HasColumnName("CoverImageURL");
    }
}
