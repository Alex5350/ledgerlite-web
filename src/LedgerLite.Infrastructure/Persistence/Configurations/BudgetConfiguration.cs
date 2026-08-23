using LedgerLite.Domain.Budgets;
using LedgerLite.Domain.FiscalPeriods;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LedgerLite.Infrastructure.Persistence.Configurations;

internal sealed class BudgetConfiguration : IEntityTypeConfiguration<Budget>
{
    public void Configure(EntityTypeBuilder<Budget> builder)
    {
        builder.ToTable("Budgets");

        builder.HasKey(b => b.Id);

        builder.HasOne<FiscalPeriod>()
            .WithMany()
            .HasForeignKey(b => b.FiscalPeriodId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(b => b.Category).HasMaxLength(100).IsRequired();

        // Money value object -> two columns: Amount (decimal) + Currency (string).
        builder.ComplexProperty(
            b => b.Limit,
            money =>
            {
                money.Property(m => m.Amount).HasColumnName("LimitAmount").HasColumnType("decimal(18,2)").IsRequired();
                money.Property(m => m.Currency).HasColumnName("LimitCurrency").HasMaxLength(3).IsRequired();
            });

        builder.Property(b => b.NotifiedThresholds).HasConversion<int>().IsRequired();

        // One budget per category per fiscal period.
        builder.HasIndex(b => new { b.FiscalPeriodId, b.Category }).IsUnique();
    }
}
