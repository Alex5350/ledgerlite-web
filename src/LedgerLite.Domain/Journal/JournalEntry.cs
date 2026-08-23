using System.Diagnostics.CodeAnalysis;
using LedgerLite.Domain.Common;
using LedgerLite.Domain.Events;
using LedgerLite.Domain.FiscalPeriods;

namespace LedgerLite.Domain.Journal;

/// <summary>Input for creating a journal entry line: exactly one of debit/credit must be positive.</summary>
public sealed record JournalEntryLineInput(Guid AccountId, decimal Debit, decimal Credit);

/// <summary>
/// Journal entry aggregate root. Enforces the double-entry invariants:
/// at least two lines, each line has exactly one positive side, and total debits equal total credits.
/// </summary>
public sealed class JournalEntry : Entity
{
    private readonly List<EntryLine> _lines = [];

    private JournalEntry(Guid fiscalPeriodId, string? description, DateTime occurredOnUtc)
    {
        FiscalPeriodId = fiscalPeriodId;
        Description = description;
        OccurredOn = occurredOnUtc;
    }

    /// <summary>
    /// Persistence-only constructor. EF Core binds constructor parameters by property name
    /// ('occurredOnUtc' does not match <see cref="OccurredOn"/>), so hydrated aggregates are
    /// populated through their backing fields via this constructor.
    /// </summary>
    private JournalEntry()
    {
    }

    public Guid FiscalPeriodId { get; }

    public string? Description { get; }

    /// <summary>UTC instant the transaction occurred.</summary>
    public DateTime OccurredOn { get; }

    public bool IsPosted { get; private set; }

    public IReadOnlyList<EntryLine> Lines => _lines.AsReadOnly();

    public decimal TotalDebits => _lines.Sum(line => line.Debit);

    public decimal TotalCredits => _lines.Sum(line => line.Credit);

    public static bool TryCreate(
        Guid fiscalPeriodId,
        string? description,
        DateTime occurredOnUtc,
        IEnumerable<JournalEntryLineInput> lines,
        [NotNullWhen(true)] out JournalEntry? entry,
        [NotNullWhen(false)] out string? error)
    {
        if (fiscalPeriodId == Guid.Empty)
        {
            entry = null;
            error = "Journal entry must belong to a fiscal period.";
            return false;
        }

        if (occurredOnUtc.Kind != DateTimeKind.Utc)
        {
            entry = null;
            error = "Journal entry occurrence timestamp must be UTC.";
            return false;
        }

        var lineList = lines?.ToList() ?? [];
        if (lineList.Count < 2)
        {
            entry = null;
            error = "A journal entry must have at least two lines.";
            return false;
        }

        decimal totalDebits = 0;
        decimal totalCredits = 0;
        foreach (var line in lineList)
        {
            if (line.AccountId == Guid.Empty)
            {
                entry = null;
                error = "Each journal entry line must reference an account.";
                return false;
            }

            var debitIsPositive = line.Debit > 0;
            var creditIsPositive = line.Credit > 0;
            if (debitIsPositive == creditIsPositive)
            {
                entry = null;
                error = "Each journal entry line must have exactly one positive side (debit or credit).";
                return false;
            }

            if (line.Debit < 0 || line.Credit < 0)
            {
                entry = null;
                error = "Journal entry line amounts cannot be negative.";
                return false;
            }

            totalDebits += line.Debit;
            totalCredits += line.Credit;
        }

        if (totalDebits != totalCredits)
        {
            entry = null;
            error = $"Journal entry is not balanced: debits {totalDebits} != credits {totalCredits}.";
            return false;
        }

        var journalEntry = new JournalEntry(
            fiscalPeriodId,
            string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            occurredOnUtc);
        journalEntry._lines.AddRange(lineList.Select(l => EntryLine.Create(l.AccountId, l.Debit, l.Credit)));

        entry = journalEntry;
        error = null;
        return true;
    }

    /// <summary>Posts the entry, raising <see cref="JournalEntryPostedDomainEvent"/>. Fails on closed periods or re-posting.</summary>
    public bool TryPost(FiscalPeriod period, [NotNullWhen(false)] out string? error)
    {
        if (period.Id != FiscalPeriodId)
        {
            error = "Journal entry does not belong to the given fiscal period.";
            return false;
        }

        if (IsPosted)
        {
            error = "Journal entry has already been posted.";
            return false;
        }

        if (!period.IsOpen)
        {
            error = "Cannot post a journal entry to a closed fiscal period.";
            return false;
        }

        IsPosted = true;
        Raise(new JournalEntryPostedDomainEvent(
            EntryId: Id,
            FiscalPeriodId: FiscalPeriodId,
            PostedAtUtc: DateTime.UtcNow));

        error = null;
        return true;
    }
}
