using LandGuard.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LandGuard.Infrastructure.Persistence.Configurations;

public class DeedVerificationReasonConfiguration : IEntityTypeConfiguration<DeedVerificationReason>
{
    public void Configure(EntityTypeBuilder<DeedVerificationReason> builder)
    {
        builder.ToTable("DeedVerificationReason", "dbo");

        builder.HasKey(r => r.DeedVerificationReasonId);
        builder.Property(r => r.DeedVerificationReasonId).HasColumnName("DeedVerificationReasonID").ValueGeneratedOnAdd();

        builder.Property(r => r.DeedVerificationId).HasColumnName("DeedVerificationID").IsRequired();

        builder.Property(r => r.Reason)
            .HasColumnName("Reason")
            .HasColumnType("varchar(50)")
            .HasConversion<string>()
            .IsRequired();

        builder.HasIndex(r => r.DeedVerificationId).HasDatabaseName("IX_DeedVerificationReason_VerificationID");

        builder.HasOne(r => r.DeedVerification)
            .WithMany(v => v.Reasons)
            .HasForeignKey(r => r.DeedVerificationId)
            .OnDelete(DeleteBehavior.Cascade); // FK_DeedVerificationReason_DeedVerification: CASCADE, same reasoning as DeedVerificationField.
    }
}
