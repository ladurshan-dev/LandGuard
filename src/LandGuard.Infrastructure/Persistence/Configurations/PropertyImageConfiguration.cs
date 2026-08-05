using LandGuard.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LandGuard.Infrastructure.Persistence.Configurations;

public class PropertyImageConfiguration : IEntityTypeConfiguration<PropertyImage>
{
    public void Configure(EntityTypeBuilder<PropertyImage> builder)
    {
        builder.ToTable("PropertyImage", "dbo");

        builder.HasKey(i => i.ImageId);
        builder.Property(i => i.ImageId).HasColumnName("ImageID").ValueGeneratedOnAdd();

        builder.Property(i => i.PropertyId).HasColumnName("PropertyID").IsRequired();
        builder.Property(i => i.ImageUrl).HasColumnName("ImageURL").HasColumnType("nvarchar(500)").IsRequired();
        builder.Property(i => i.ImageHash).HasColumnName("ImageHash").HasColumnType("varchar(255)");
        builder.Property(i => i.IsPrimary).HasColumnName("IsPrimary").IsRequired();
        builder.Property(i => i.UploadedDate).HasColumnName("UploadedDate").HasColumnType("datetime2(0)").IsRequired();

        // Fraud rule 2 (duplicate image) lookup - filtered because a row can
        // exist before its hash is computed.
        builder.HasIndex(i => i.ImageHash)
            .HasDatabaseName("IX_PropertyImage_Hash")
            .HasFilter("[ImageHash] IS NOT NULL");

        builder.HasOne(i => i.Property)
            .WithMany(p => p.Images)
            .HasForeignKey(i => i.PropertyId)
            .OnDelete(DeleteBehavior.Cascade); // FK_PropertyImage_Property: CASCADE
    }
}
