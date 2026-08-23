using LedgerLite.Domain.Budgets;
using LedgerLite.Domain.Journal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LedgerLite.Infrastructure.Persistence.Configurations;

internal sealed class EntryLineConfiguration : IEntityTypeConfiguration<EntryLine>
{
    public void Configure(EntityTypeBuilder<EntryLine> builder)
    {
        builder.ToTable("EntryLines");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.AccountId).IsRequired();

        builder.Property(l => l.Debit).HasColumnType("decimal(18,2)").IsRequired();

        builder.Property(l => l.Credit).HasColumnType("decimal(18,2)").IsRequired();

        builder.HasIndex(l => l.AccountId);
    }
}
