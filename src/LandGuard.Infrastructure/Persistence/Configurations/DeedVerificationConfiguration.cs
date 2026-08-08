using LandGuard.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LandGuard.Infrastructure.Persistence.Configurations;

public class DeedVerificationConfiguration : IEntityTypeConfiguration<DeedVerification>
{
    public void Configure(EntityTypeBuilder<DeedVerification> builder)
    {
        builder.ToTable("DeedVerification", "dbo");

        builder.HasKey(v => v.DeedVerificationId);
        builder.Property(v => v.DeedVerificationId).HasColumnName("DeedVerificationID").ValueGeneratedOnAdd();

        builder.Property(v => v.PropertyId).HasColumnName("PropertyID").IsRequired();
        builder.Property(v => v.SubmittedByUserId).HasColumnName("SubmittedByUserID").IsRequired();
        builder.Property(v => v.GovernmentRecordId).HasColumnName("GovernmentRecordID").HasColumnType("varchar(20)");
        builder.Property(v => v.GovernmentRecordStatus).HasColumnName("GovernmentRecordStatus").HasColumnType("varchar(20)");
        builder.Property(v => v.Summary).HasColumnName("Summary").HasColumnType("nvarchar(max)");
        builder.Property(v => v.SellerDocumentReference).HasColumnName("SellerDocumentReference").HasColumnType("varchar(255)");
        builder.Property(v => v.VerifiedDate).HasColumnName("VerifiedDate").HasColumnType("datetime2(0)").IsRequired();

        builder.Property(v => v.VerificationStatus)
            .HasColumnName("VerificationStatus")
            .HasColumnType("varchar(30)")
            .HasConversion<string>()
            .IsRequired();

        // History-by-property lookup (usp_DeedVerification_GetHistory relies on this shape) - mirrors IX_FraudCheck_Property_Date.
        builder.HasIndex(v => new { v.PropertyId, v.VerifiedDate })
            .HasDatabaseName("IX_DeedVerification_Property_Date");

        // One-directional FKs: Property.cs/User.cs are not modified by this
        // phase, so no reverse collection navigation is added there -
        // WithMany() with no navigation expression configures the FK
        // without requiring a matching collection property on the
        // principal side.
        builder.HasOne(v => v.Property)
            .WithMany()
            .HasForeignKey(v => v.PropertyId)
            .OnDelete(DeleteBehavior.Restrict); // FK_DeedVerification_Property: NO ACTION - verification history must survive a property's own deletion path, never cascade-deleted silently.

        builder.HasOne(v => v.SubmittedByUser)
            .WithMany()
            .HasForeignKey(v => v.SubmittedByUserId)
            .OnDelete(DeleteBehavior.Restrict); // FK_DeedVerification_Users: NO ACTION, matching FK_Property_Seller - users are suspended, never deleted.
    }
}
