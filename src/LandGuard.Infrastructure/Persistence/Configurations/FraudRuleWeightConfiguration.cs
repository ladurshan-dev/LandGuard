using LandGuard.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LandGuard.Infrastructure.Persistence.Configurations;

public class FraudRuleWeightConfiguration : IEntityTypeConfiguration<FraudRuleWeight>
{
    public void Configure(EntityTypeBuilder<FraudRuleWeight> builder)
    {
        builder.ToTable("FraudRuleWeight", "dbo");

        // Natural string primary key, not an identity column - matches
        // PK_FraudRuleWeight exactly.
        builder.HasKey(w => w.RuleCode);
        builder.Property(w => w.RuleCode).HasColumnName("RuleCode").HasColumnType("varchar(30)").ValueGeneratedNever();

        builder.Property(w => w.RuleName).HasColumnName("RuleName").HasColumnType("nvarchar(100)").IsRequired();
        builder.Property(w => w.Weight).HasColumnName("Weight").IsRequired();
        builder.Property(w => w.Threshold).HasColumnName("Threshold").HasColumnType("decimal(9,4)");
        builder.Property(w => w.IsEnabled).HasColumnName("IsEnabled").IsRequired();
        builder.Property(w => w.Description).HasColumnName("Description").HasColumnType("nvarchar(400)");

        // No relationships: FraudCheck stores per-rule outcomes as bit
        // columns, not a RuleCode foreign key - the join only happens
        // logically, inside vw_FraudCheckDetail.
    }
}
