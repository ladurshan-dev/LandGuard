using LandGuard.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LandGuard.Infrastructure.Persistence.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notification", "dbo");

        builder.HasKey(n => n.NotificationId);
        builder.Property(n => n.NotificationId).HasColumnName("NotificationID").ValueGeneratedOnAdd();

        builder.Property(n => n.UserId).HasColumnName("UserID").IsRequired();
        builder.Property(n => n.Message).HasColumnName("Message").HasColumnType("nvarchar(500)").IsRequired();
        builder.Property(n => n.NotificationDate).HasColumnName("NotificationDate").HasColumnType("datetime2(0)").IsRequired();
        builder.Property(n => n.RelatedPropertyId).HasColumnName("RelatedPropertyID");

        builder.Property(n => n.Status)
            .HasColumnName("Status")
            .HasColumnType("varchar(20)")
            .HasConversion<string>()
            .IsRequired();

        // Notification bell: unread first, newest first.
        builder.HasIndex(n => new { n.UserId, n.Status, n.NotificationDate })
            .HasDatabaseName("IX_Notification_User_Status");

        builder.HasOne(n => n.User)
            .WithMany(u => u.Notifications)
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade); // FK_Notification_User: CASCADE

        builder.HasOne(n => n.RelatedProperty)
            .WithMany(p => p.RelatedNotifications)
            .HasForeignKey(n => n.RelatedPropertyId)
            .OnDelete(DeleteBehavior.Restrict); // FK_Notification_Property: NO ACTION. usp_Property_Delete nulls this column first.
    }
}
