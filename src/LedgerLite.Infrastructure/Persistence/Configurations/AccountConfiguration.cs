using LedgerLite.Domain.Accounts;
using LedgerLite.Domain.FiscalPeriods;
using LedgerLite.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LedgerLite.Infrastructure.Persistence.Configurations;

internal sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("Accounts");

        builder.HasKey(a => a.Id);

        // AccountNumber value object -> single string column.
        builder.Property(a => a.Number)
            .HasConversion(
                number => number.Value,
                value => AccountNumber.Create(value))
            .HasMaxLength(4)
            .IsFixedLength()
            .IsRequired();

        builder.Property(a => a.Name).HasMaxLength(100).IsRequired();

        builder.Property(a => a.Type).HasConversion<int>().IsRequired();

        // Accounts belong to a fiscal period (no inverse navigation on the aggregate).
        builder.HasOne<FiscalPeriod>()
            .WithMany()
            .HasForeignKey(a => a.FiscalPeriodId)
            .OnDelete(DeleteBehavior.Restrict);

        // An account number is unique within its fiscal period.
        builder.HasIndex(a => new { a.FiscalPeriodId, a.Number }).IsUnique();
    }
}
