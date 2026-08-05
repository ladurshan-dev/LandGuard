using LandGuard.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LandGuard.Infrastructure.Persistence.Configurations;

public class PodcastConfiguration : IEntityTypeConfiguration<Podcast>
{
    public void Configure(EntityTypeBuilder<Podcast> builder)
    {
        builder.ToTable("Podcast", "dbo");

        builder.HasKey(p => p.PodcastId);
        builder.Property(p => p.PodcastId).HasColumnName("PodcastID").ValueGeneratedOnAdd();

        builder.Property(p => p.AdminId).HasColumnName("AdminID").IsRequired();
        builder.Property(p => p.Title).HasColumnName("Title").HasColumnType("nvarchar(200)").IsRequired();
        builder.Property(p => p.Description).HasColumnName("Description").HasColumnType("nvarchar(max)");
        builder.Property(p => p.AudioUrl).HasColumnName("AudioURL").HasColumnType("nvarchar(500)").IsRequired();
        builder.Property(p => p.UploadDate).HasColumnName("UploadDate").HasColumnType("datetime2(0)").IsRequired();

        builder.Property(p => p.Language)
            .HasColumnName("Language")
            .HasColumnType("varchar(50)")
            .HasConversion<string>()
            .IsRequired();

        builder.HasOne(p => p.Admin)
            .WithMany(u => u.PodcastsUploaded)
            .HasForeignKey(p => p.AdminId)
            .OnDelete(DeleteBehavior.Restrict); // FK_Podcast_Admin: NO ACTION
    }
}
