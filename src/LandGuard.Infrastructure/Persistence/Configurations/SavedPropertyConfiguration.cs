using LandGuard.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LandGuard.Infrastructure.Persistence.Configurations;

public class SavedPropertyConfiguration : IEntityTypeConfiguration<SavedProperty>
{
    public void Configure(EntityTypeBuilder<SavedProperty> builder)
    {
        builder.ToTable("SavedProperty", "dbo");

        builder.HasKey(s => s.SavedPropertyId);
        builder.Property(s => s.SavedPropertyId).HasColumnName("SavedPropertyID").ValueGeneratedOnAdd();

        builder.Property(s => s.BuyerId).HasColumnName("BuyerID").IsRequired();
        builder.Property(s => s.PropertyId).HasColumnName("PropertyID").IsRequired();
        builder.Property(s => s.SavedDate).HasColumnName("SavedDate").HasColumnType("datetime2(0)").IsRequired();

        // UQ_SavedProperty_Pair - a buyer can save a listing only once.
        builder.HasIndex(s => new { s.BuyerId, s.PropertyId })
            .IsUnique()
            .HasDatabaseName("UQ_SavedProperty_Pair");

        builder.HasOne(s => s.Buyer)
            .WithMany(u => u.SavedProperties)
            .HasForeignKey(s => s.BuyerId)
            .OnDelete(DeleteBehavior.Restrict); // FK_SavedProperty_Buyer: NO ACTION

        builder.HasOne(s => s.Property)
            .WithMany(p => p.SavedByBuyers)
            .HasForeignKey(s => s.PropertyId)
            .OnDelete(DeleteBehavior.Cascade); // FK_SavedProperty_Property: CASCADE
    }
}
