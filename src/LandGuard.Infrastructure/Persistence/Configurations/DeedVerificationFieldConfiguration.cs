using LandGuard.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LandGuard.Infrastructure.Persistence.Configurations;

public class DeedVerificationFieldConfiguration : IEntityTypeConfiguration<DeedVerificationField>
{
    public void Configure(EntityTypeBuilder<DeedVerificationField> builder)
    {
        builder.ToTable("DeedVerificationField", "dbo");

        builder.HasKey(f => f.DeedVerificationFieldId);
        builder.Property(f => f.DeedVerificationFieldId).HasColumnName("DeedVerificationFieldID").ValueGeneratedOnAdd();

        builder.Property(f => f.DeedVerificationId).HasColumnName("DeedVerificationID").IsRequired();
        builder.Property(f => f.FieldName).HasColumnName("FieldName").HasColumnType("varchar(30)").IsRequired();
        builder.Property(f => f.GovernmentValue).HasColumnName("GovernmentValue").HasColumnType("nvarchar(255)");
        builder.Property(f => f.SellerValue).HasColumnName("SellerValue").HasColumnType("nvarchar(255)");
        builder.Property(f => f.IsMatch).HasColumnName("IsMatch").IsRequired();
        builder.Property(f => f.Message).HasColumnName("Message").HasColumnType("nvarchar(400)");

        builder.HasIndex(f => f.DeedVerificationId).HasDatabaseName("IX_DeedVerificationField_VerificationID");

        builder.HasOne(f => f.DeedVerification)
            .WithMany(v => v.Fields)
            .HasForeignKey(f => f.DeedVerificationId)
            .OnDelete(DeleteBehavior.Cascade); // FK_DeedVerificationField_DeedVerification: CASCADE - evidence has no meaning without its parent run.
    }
}
