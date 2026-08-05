using LandGuard.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LandGuard.Infrastructure.Persistence.Configurations;

public class PropertyConfiguration : IEntityTypeConfiguration<Property>
{
    public void Configure(EntityTypeBuilder<Property> builder)
    {
        builder.ToTable("Property", "dbo");

        builder.HasKey(p => p.PropertyId);
        builder.Property(p => p.PropertyId).HasColumnName("PropertyID").ValueGeneratedOnAdd();

        builder.Property(p => p.SellerId).HasColumnName("SellerID").IsRequired();
        builder.Property(p => p.Title).HasColumnName("Title").HasColumnType("nvarchar(200)").IsRequired();
        builder.Property(p => p.Description).HasColumnName("Description").HasColumnType("nvarchar(max)");
        builder.Property(p => p.Location).HasColumnName("Location").HasColumnType("nvarchar(255)").IsRequired();
        builder.Property(p => p.District).HasColumnName("District").HasColumnType("nvarchar(100)");
        builder.Property(p => p.Latitude).HasColumnName("Latitude").HasColumnType("decimal(9,6)");
        builder.Property(p => p.Longitude).HasColumnName("Longitude").HasColumnType("decimal(9,6)");
        builder.Property(p => p.Size).HasColumnName("Size").HasColumnType("float").IsRequired();
        builder.Property(p => p.Price).HasColumnName("Price").HasColumnType("decimal(14,2)").IsRequired();
        builder.Property(p => p.DeedReference).HasColumnName("DeedReference").HasColumnType("varchar(100)");
        builder.Property(p => p.UploadDate).HasColumnName("UploadDate").HasColumnType("datetime2(0)").IsRequired();

        // PERSISTED computed column (Price / Size). EF Core must never try
        // to write it - ValueGeneratedOnAddOrUpdate marks it database-generated
        // on both insert and update, matching a persisted computed column.
        builder.Property(p => p.PricePerPerch)
            .HasColumnName("PricePerPerch")
            .HasColumnType("decimal(14,2)")
            .ValueGeneratedOnAddOrUpdate();

        builder.Property(p => p.Status)
            .HasColumnName("Status")
            .HasColumnType("varchar(20)")
            .HasConversion<string>()
            .IsRequired();

        // Fraud rule 4 (deed duplicate) lookup - filtered because draft
        // listings may still have a NULL DeedReference.
        builder.HasIndex(p => p.DeedReference)
            .HasDatabaseName("IX_Property_DeedReference")
            .HasFilter("[DeedReference] IS NOT NULL");

        builder.HasOne(p => p.Seller)
            .WithMany(u => u.Properties)
            .HasForeignKey(p => p.SellerId)
            .OnDelete(DeleteBehavior.Restrict); // FK_Property_Seller: NO ACTION. Users are suspended, never deleted.
    }
}
