using LandGuard.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LandGuard.Infrastructure.Persistence.Configurations;

public class PriceBenchmarkConfiguration : IEntityTypeConfiguration<PriceBenchmark>
{
    public void Configure(EntityTypeBuilder<PriceBenchmark> builder)
    {
        builder.ToTable("PriceBenchmark", "dbo");

        builder.HasKey(b => b.BenchmarkId);
        builder.Property(b => b.BenchmarkId).HasColumnName("BenchmarkID").ValueGeneratedOnAdd();

        builder.Property(b => b.District).HasColumnName("District").HasColumnType("nvarchar(100)").IsRequired();
        builder.Property(b => b.MarketPricePerPerch).HasColumnName("MarketPricePerPerch").HasColumnType("decimal(14,2)").IsRequired();
        builder.Property(b => b.UpdatedDate).HasColumnName("UpdatedDate").HasColumnType("datetime2(0)").IsRequired();

        builder.HasIndex(b => b.District).IsUnique().HasDatabaseName("UQ_PriceBenchmark_District");

        // No relationships: the link to Property.District is a same-named
        // string match evaluated in T-SQL, not a physical foreign key.
    }
}
