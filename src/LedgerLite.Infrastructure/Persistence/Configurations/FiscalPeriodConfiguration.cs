using LedgerLite.Domain.FiscalPeriods;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LedgerLite.Infrastructure.Persistence.Configurations;

internal sealed class FiscalPeriodConfiguration : IEntityTypeConfiguration<FiscalPeriod>
{
    public void Configure(EntityTypeBuilder<FiscalPeriod> builder)
    {
        builder.ToTable("FiscalPeriods");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).HasMaxLength(100).IsRequired();

        // DateOnly is stored as ISO-8601 text by the SQLite provider.
        builder.Property(p => p.StartDate).IsRequired();
        builder.Property(p => p.EndDate).IsRequired();

        builder.Property(p => p.Status).HasConversion<int>().IsRequired();

        builder.HasIndex(p => new { p.StartDate, p.EndDate });
    }
}
