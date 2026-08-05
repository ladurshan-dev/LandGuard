using LandGuard.Domain.ReadModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LandGuard.Infrastructure.Persistence.Configurations;

public class RuleTriggerFrequencyConfiguration : IEntityTypeConfiguration<RuleTriggerFrequency>
{
    public void Configure(EntityTypeBuilder<RuleTriggerFrequency> builder)
    {
        builder.HasNoKey();
        builder.ToView("vw_RuleTriggerFrequency", "dbo");

        builder.Property(r => r.RuleCode).HasColumnType("varchar(30)");
        builder.Property(r => r.RuleName).HasColumnType("nvarchar(100)");
        builder.Property(r => r.TriggerRatePercent).HasColumnType("decimal(5,2)");
    }
}
