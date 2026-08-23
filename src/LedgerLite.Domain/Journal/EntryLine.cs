using LedgerLite.Domain.Common;

namespace LedgerLite.Domain.Journal;

/// <summary>
/// A single line of a journal entry. Exactly one side (debit or credit) must be positive;
/// the other is zero. Invariant enforced by <see cref="JournalEntry.TryCreate"/>.
/// </summary>
public sealed class EntryLine : Entity
{
    private EntryLine(Guid accountId, decimal debit, decimal credit)
    {
        AccountId = accountId;
        Debit = debit;
        Credit = credit;
    }

    public Guid AccountId { get; }

    public decimal Debit { get; }

    public decimal Credit { get; }

    internal static EntryLine Create(Guid accountId, decimal debit, decimal credit) =>
        new(accountId, debit, credit);
}
