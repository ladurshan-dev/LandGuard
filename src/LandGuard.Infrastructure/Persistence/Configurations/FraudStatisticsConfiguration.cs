using LandGuard.Domain.ReadModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LandGuard.Infrastructure.Persistence.Configurations;

public class FraudStatisticsConfiguration : IEntityTypeConfiguration<FraudStatistics>
{
    public void Configure(EntityTypeBuilder<FraudStatistics> builder)
    {
        builder.HasNoKey();
        builder.ToView("vw_FraudStatistics", "dbo");

        builder.Property(s => s.AverageRiskScore).HasColumnType("decimal(5,2)");
    }
}
