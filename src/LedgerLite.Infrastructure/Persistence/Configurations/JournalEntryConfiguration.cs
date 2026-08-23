using LedgerLite.Domain.FiscalPeriods;
using LedgerLite.Domain.Journal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LedgerLite.Infrastructure.Persistence.Configurations;

internal sealed class JournalEntryConfiguration : IEntityTypeConfiguration<JournalEntry>
{
    public void Configure(EntityTypeBuilder<JournalEntry> builder)
    {
        builder.ToTable("JournalEntries");

        builder.HasKey(e => e.Id);

        builder.HasOne<FiscalPeriod>()
            .WithMany()
            .HasForeignKey(e => e.FiscalPeriodId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(e => e.Description).HasMaxLength(500);

        // Preserve DateTimeKind.Utc across the TEXT round-trip on SQLite.
        builder.Property(e => e.OccurredOn)
            .HasConversion(
                value => value.ToUniversalTime(),
                value => DateTime.SpecifyKind(value, DateTimeKind.Utc))
            .IsRequired();

        builder.Property(e => e.IsPosted).IsRequired();

        builder.HasIndex(e => new { e.FiscalPeriodId, e.IsPosted });

        // Lines are part of the aggregate; the backing list field is the access mode.
        builder.Metadata.FindNavigation(nameof(JournalEntry.Lines))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(e => e.Lines)
            .WithOne()
            .HasForeignKey("JournalEntryId")
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
    }
}
