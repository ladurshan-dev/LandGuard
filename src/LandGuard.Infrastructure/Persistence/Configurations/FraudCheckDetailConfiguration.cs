using LandGuard.Domain.ReadModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LandGuard.Infrastructure.Persistence.Configurations;

public class FraudCheckDetailConfiguration : IEntityTypeConfiguration<FraudCheckDetail>
{
    public void Configure(EntityTypeBuilder<FraudCheckDetail> builder)
    {
        builder.HasNoKey();
        builder.ToView("vw_FraudCheckDetail", "dbo");

        builder.Property(d => d.PropertyId).HasColumnName("PropertyID");
        builder.Property(d => d.FraudCheckId).HasColumnName("FraudCheckID");
        builder.Property(d => d.RuleCode).HasColumnType("varchar(30)");
        builder.Property(d => d.RuleName).HasColumnType("nvarchar(100)");
        builder.Property(d => d.Description).HasColumnType("nvarchar(400)");
    }
}
