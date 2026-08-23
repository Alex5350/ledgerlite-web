using LedgerLite.Domain.Events;
using LedgerLite.Domain.FiscalPeriods;
using LedgerLite.Domain.Journal;

namespace LedgerLite.Domain.UnitTests.Journal;

public sealed class JournalEntryTests
{
    private static readonly Guid PeriodId = Guid.CreateVersion7();
    private static readonly Guid DebitAccountId = Guid.CreateVersion7();
    private static readonly Guid CreditAccountId = Guid.CreateVersion7();
    private static readonly DateTime OccurredOnUtc = new(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);

    private static JournalEntryLineInput Line(Guid accountId, decimal debit = 0m, decimal credit = 0m) =>
        new(accountId, debit, credit);

    private static IEnumerable<JournalEntryLineInput> BalancedLines() =>
    [
        Line(DebitAccountId, 100m),
        Line(CreditAccountId, credit: 100m)
    ];

    private static bool TryCreate(
        IEnumerable<JournalEntryLineInput>? lines,
        out JournalEntry? entry,
        out string? error,
        Guid? periodId = null,
        DateTime? occurredOn = null) =>
        JournalEntry.TryCreate(
            periodId ?? PeriodId,
            "Test entry",
            occurredOn ?? OccurredOnUtc,
            lines!,
            out entry,
            out error);

    private static FiscalPeriod OpenPeriod()
    {
        if (!FiscalPeriod.TryCreate("January 2026", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), out var period, out var error))
        {
            throw new InvalidOperationException(error);
        }

        return period!;
    }

    public sealed class Create
    {
        [Fact]
        public void WithBalancedLines_Succeeds()
        {
            var created = TryCreate(BalancedLines(), out var entry, out var error);

            Assert.True(created);
            Assert.Null(error);
            Assert.NotNull(entry);
            Assert.Equal(PeriodId, entry!.FiscalPeriodId);
            Assert.Equal("Test entry", entry.Description);
            Assert.Equal(OccurredOnUtc, entry.OccurredOn);
            Assert.False(entry.IsPosted);
            Assert.Equal(2, entry.Lines.Count);
            Assert.Equal(100m, entry.TotalDebits);
            Assert.Equal(100m, entry.TotalCredits);
        }

        [Fact]
        public void WithBalancedLinesAcrossManyAccounts_Succeeds()
        {
            var thirdAccount = Guid.CreateVersion7();
            var lines = new[]
            {
                Line(DebitAccountId, 60m),
                Line(CreditAccountId, credit: 25m),
                Line(thirdAccount, credit: 35m)
            };

            var created = TryCreate(lines, out var entry, out _);

            Assert.True(created);
            Assert.Equal(3, entry!.Lines.Count);
            Assert.Equal(60m, entry.TotalDebits);
            Assert.Equal(60m, entry.TotalCredits);
        }

        [Fact]
        public void WithDuplicateAccountOnBothSides_Succeeds()
        {
            var lines = new[]
            {
                Line(DebitAccountId, 40m),
                Line(DebitAccountId, 60m),
                Line(CreditAccountId, credit: 100m)
            };

            var created = TryCreate(lines, out var entry, out _);

            Assert.True(created);
            Assert.Equal(3, entry!.Lines.Count);
        }

        [Fact]
        public void WithNullLines_Fails()
        {
            var created = TryCreate(null, out var entry, out var error);

            Assert.False(created);
            Assert.Null(entry);
            Assert.Contains("at least two lines", error);
        }

        [Fact]
        public void WithEmptyLines_Fails()
        {
            var created = TryCreate([], out var entry, out var error);

            Assert.False(created);
            Assert.Contains("at least two lines", error);
        }

        [Fact]
        public void WithSingleLine_Fails()
        {
            var created = TryCreate([Line(DebitAccountId, 100m)], out var entry, out var error);

            Assert.False(created);
            Assert.Null(entry);
            Assert.Contains("at least two lines", error);
        }

        [Fact]
        public void WithEmptyPeriodId_Fails()
        {
            var created = TryCreate(BalancedLines(), out var entry, out var error, periodId: Guid.Empty);

            Assert.False(created);
            Assert.Contains("fiscal period", error);
        }

        [Fact]
        public void WithNonUtcTimestamp_Fails()
        {
            var local = new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Unspecified);

            var created = TryCreate(BalancedLines(), out var entry, out var error, occurredOn: local);

            Assert.False(created);
            Assert.Null(entry);
            Assert.Contains("UTC", error);
        }

        [Fact]
        public void WithLineMissingAccount_Fails()
        {
            var lines = new[] { Line(Guid.Empty, 100m), Line(CreditAccountId, credit: 100m) };

            var created = TryCreate(lines, out var entry, out var error);

            Assert.False(created);
            Assert.Contains("reference an account", error);
        }

        [Fact]
        public void WithBothSidesPositive_Fails()
        {
            var lines = new[] { Line(DebitAccountId, 100m, 100m), Line(CreditAccountId, credit: 200m) };

            var created = TryCreate(lines, out _, out var error);

            Assert.False(created);
            Assert.Contains("exactly one positive side", error);
        }

        [Fact]
        public void WithBothSidesZero_Fails()
        {
            var lines = new[] { Line(DebitAccountId), Line(CreditAccountId) };

            var created = TryCreate(lines, out _, out var error);

            Assert.False(created);
            Assert.Contains("exactly one positive side", error);
        }

        [Fact]
        public void WithNegativeDebit_Fails()
        {
            var lines = new[] { Line(DebitAccountId, -5m, 10m), Line(CreditAccountId, credit: 5m) };

            var created = TryCreate(lines, out _, out var error);

            Assert.False(created);
            Assert.Contains("cannot be negative", error);
        }

        [Fact]
        public void WithUnbalancedLines_Fails()
        {
            var lines = new[] { Line(DebitAccountId, 100m), Line(CreditAccountId, credit: 90m) };

            var created = TryCreate(lines, out var entry, out var error);

            Assert.False(created);
            Assert.Null(entry);
            Assert.Contains("not balanced", error);
            Assert.Contains("100", error);
            Assert.Contains("90", error);
        }

        [Fact]
        public void WithWhitespaceDescription_StoresNull()
        {
            if (!JournalEntry.TryCreate(PeriodId, "   ", OccurredOnUtc, BalancedLines(), out var entry, out _))
            {
                Assert.Fail("Entry should be created");
            }

            Assert.Null(entry!.Description);
        }

        [Fact]
        public void WithPaddedDescription_Trims()
        {
            if (!JournalEntry.TryCreate(PeriodId, "  payroll  ", OccurredOnUtc, BalancedLines(), out var entry, out _))
            {
                Assert.Fail("Entry should be created");
            }

            Assert.Equal("payroll", entry!.Description);
        }
    }

    public sealed class Post
    {
        private static (JournalEntry Entry, FiscalPeriod Period) UnpostedEntry()
        {
            var period = OpenPeriod();
            if (!JournalEntry.TryCreate(period.Id, null, OccurredOnUtc, BalancedLines(), out var entry, out var error))
            {
                throw new InvalidOperationException(error);
            }

            return (entry!, period);
        }

        [Fact]
        public void WhenPeriodIsOpen_PostsAndRaisesEvent()
        {
            var (entry, period) = UnpostedEntry();

            var posted = entry.TryPost(period, out var error);

            Assert.True(posted);
            Assert.Null(error);
            Assert.True(entry.IsPosted);
            var domainEvent = Assert.Single(entry.DomainEvents);
            var postedEvent = Assert.IsType<JournalEntryPostedDomainEvent>(domainEvent);
            Assert.Equal(entry.Id, postedEvent.EntryId);
            Assert.Equal(period.Id, postedEvent.FiscalPeriodId);
        }

        [Fact]
        public void WhenEntryBelongsToAnotherPeriod_ReturnsError()
        {
            var (entry, _) = UnpostedEntry();
            var otherPeriod = OpenPeriod();

            var posted = entry.TryPost(otherPeriod, out var error);

            Assert.False(posted);
            Assert.Contains("does not belong", error);
            Assert.False(entry.IsPosted);
            Assert.Empty(entry.DomainEvents);
        }

        [Fact]
        public void WhenPeriodIsClosed_ReturnsError()
        {
            var (entry, period) = UnpostedEntry();
            if (!period.TryClose(new DateOnly(2026, 2, 1), out var closeError))
            {
                Assert.Fail($"Closing failed: {closeError}");
            }

            var posted = entry.TryPost(period, out var error);

            Assert.False(posted);
            Assert.Contains("closed fiscal period", error);
            Assert.False(entry.IsPosted);
            Assert.Empty(entry.DomainEvents);
        }

        [Fact]
        public void WhenAlreadyPosted_ReturnsErrorAndDoesNotRaiseSecondEvent()
        {
            var (entry, period) = UnpostedEntry();
            Assert.True(entry.TryPost(period, out _));

            var postedAgain = entry.TryPost(period, out var error);

            Assert.False(postedAgain);
            Assert.Contains("already been posted", error);
            Assert.Single(entry.DomainEvents);
        }
    }

    public sealed class PullEvents
    {
        [Fact]
        public void ReturnsRaisedEventsAndClearsCollection()
        {
            var period = OpenPeriod();
            if (!JournalEntry.TryCreate(period.Id, null, OccurredOnUtc, BalancedLines(), out var entry, out var error))
            {
                Assert.Fail(error);
            }

            entry!.TryPost(period, out _);
            Assert.Single(entry.DomainEvents);

            var pulled = entry.PullEvents();

            var postedEvent = Assert.IsType<JournalEntryPostedDomainEvent>(Assert.Single(pulled));
            Assert.Equal(entry.Id, postedEvent.EntryId);
            Assert.Empty(entry.DomainEvents);
            Assert.Empty(entry.PullEvents());
        }

        [Fact]
        public void WithoutRaisedEvents_ReturnsEmpty()
        {
            var period = OpenPeriod();
            if (!JournalEntry.TryCreate(period.Id, null, OccurredOnUtc, BalancedLines(), out var entry, out var error))
            {
                Assert.Fail(error);
            }

            Assert.Empty(entry!.PullEvents());
        }
    }
}
