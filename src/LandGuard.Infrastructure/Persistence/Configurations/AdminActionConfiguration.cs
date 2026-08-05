using LandGuard.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LandGuard.Infrastructure.Persistence.Configurations;

public class AdminActionConfiguration : IEntityTypeConfiguration<AdminAction>
{
    public void Configure(EntityTypeBuilder<AdminAction> builder)
    {
        builder.ToTable("AdminAction", "dbo");

        builder.HasKey(a => a.AdminActionId);
        builder.Property(a => a.AdminActionId).HasColumnName("AdminActionID").ValueGeneratedOnAdd();

        builder.Property(a => a.AdminId).HasColumnName("AdminID").IsRequired();
        builder.Property(a => a.PropertyId).HasColumnName("PropertyID");
        builder.Property(a => a.TargetUserId).HasColumnName("TargetUserID");
        builder.Property(a => a.ReportId).HasColumnName("ReportID");
        builder.Property(a => a.Remarks).HasColumnName("Remarks").HasColumnType("nvarchar(500)");
        builder.Property(a => a.ActionDate).HasColumnName("ActionDate").HasColumnType("datetime2(0)").IsRequired();

        builder.Property(a => a.ActionType)
            .HasColumnName("ActionType")
            .HasColumnType("varchar(30)")
            .HasConversion<string>()
            .IsRequired();

        builder.HasIndex(a => new { a.PropertyId, a.ActionDate }).HasDatabaseName("IX_AdminAction_Property");
        builder.HasIndex(a => new { a.AdminId, a.ActionDate }).HasDatabaseName("IX_AdminAction_Admin_Date");

        // Two independent FKs to Users on this table (AdminId, TargetUserId),
        // so each needs its own paired nav collection on User
        // (AdminActionsPerformed / AdminActionsReceived) - EF Core can't infer
        // which is which by convention with two FKs to the same principal.
        builder.HasOne(a => a.Admin)
            .WithMany(u => u.AdminActionsPerformed)
            .HasForeignKey(a => a.AdminId)
            .OnDelete(DeleteBehavior.Restrict); // FK_AdminAction_Admin: NO ACTION

        builder.HasOne(a => a.TargetUser)
            .WithMany(u => u.AdminActionsReceived)
            .HasForeignKey(a => a.TargetUserId)
            .OnDelete(DeleteBehavior.Restrict); // FK_AdminAction_TargetUsr: NO ACTION

        builder.HasOne(a => a.Property)
            .WithMany(p => p.AdminActions)
            .HasForeignKey(a => a.PropertyId)
            .OnDelete(DeleteBehavior.Restrict); // FK_AdminAction_Property: NO ACTION. usp_Property_Delete nulls this column first.

        builder.HasOne(a => a.SuspiciousReport)
            .WithMany(s => s.AdminActions)
            .HasForeignKey(a => a.ReportId)
            .OnDelete(DeleteBehavior.Restrict); // FK_AdminAction_Report: NO ACTION
    }
}
