using LandGuard.Domain.Entities;
using LandGuard.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LandGuard.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users", "dbo");

        builder.HasKey(u => u.UserId);
        builder.Property(u => u.UserId).HasColumnName("UserID").ValueGeneratedOnAdd();

        builder.Property(u => u.Name).HasColumnName("Name").HasColumnType("nvarchar(150)").IsRequired();
        builder.Property(u => u.Email).HasColumnName("Email").HasColumnType("nvarchar(150)").IsRequired();
        builder.Property(u => u.PasswordHash).HasColumnName("PasswordHash").HasColumnType("nvarchar(255)").IsRequired();
        builder.Property(u => u.Nic).HasColumnName("NIC").HasColumnType("varchar(20)");
        builder.Property(u => u.Phone).HasColumnName("Phone").HasColumnType("varchar(20)");
        builder.Property(u => u.CreatedAt).HasColumnName("CreatedAt").HasColumnType("datetime2(0)").IsRequired();
        builder.Property(u => u.IsActive).HasColumnName("IsActive").IsRequired();
        builder.Property(u => u.NicVerified).HasColumnName("NICVerified").IsRequired();

        // dbo.Users.Role is VARCHAR(20) constrained by CK_Users_Role to the
        // literal strings 'Buyer' / 'Seller' / 'Admin'. UserRole.Administrator
        // is kept as the C# name (see the enum's doc comment) and mapped to
        // "Admin" here, rather than renaming the enum member, so every other
        // layer can keep using the friendlier name.
        builder.Property(u => u.Role)
            .HasColumnName("Role")
            .HasColumnType("varchar(20)")
            .HasConversion(
                role => role == UserRole.Administrator ? "Admin" : role.ToString(),
                value => value == "Admin" ? UserRole.Administrator : Enum.Parse<UserRole>(value))
            .IsRequired();

        builder.HasIndex(u => u.Email).IsUnique().HasDatabaseName("UQ_Users_Email");

        // NIC uniqueness is a FILTERED unique index in the database
        // (WHERE NIC IS NOT NULL) so buyers without a NIC aren't blocked.
        // EF Core supports filtered indexes directly.
        builder.HasIndex(u => u.Nic)
            .IsUnique()
            .HasDatabaseName("UX_Users_NIC")
            .HasFilter("[NIC] IS NOT NULL");

        // Relationships where Users is the principal are configured on the
        // dependent side (PropertyConfiguration, NotificationConfiguration,
        // etc.) so each FK's OnDelete behavior sits next to the column it
        // belongs to. Nothing to configure here beyond the navigations
        // themselves, which EF Core infers from the paired HasForeignKey calls.
    }
}
