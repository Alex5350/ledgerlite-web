using Bunit;
using LedgerLite.Web.Client.Pages;
using LedgerLite.Web.Client.Services.Api;
using LedgerLite.Web.Tests.Infrastructure;

namespace LedgerLite.Web.Tests.Components;

/// <summary>
/// Covers the Journal page's post-entry editor: the modal's live balance pill and the
/// footer post button, which stays disabled until the drafted lines balance.
/// </summary>
public sealed class JournalPageTests
{
    private sealed record JournalFixture(
        AppTestContext Context,
        IRenderedComponent<Journal> Cut,
        FiscalPeriodResponse Period,
        AccountResponse Cash,
        AccountResponse Equity) : IDisposable
    {
        public void Dispose() => Context.Dispose();
    }

    private static JournalFixture RenderJournal(IReadOnlyList<JournalEntryResponse>? entries = null)
    {
        var ctx = new AppTestContext();
        var period = new FiscalPeriodResponse(
            Id: Guid.NewGuid(),
            Name: "2026 Q1",
            StartDate: new DateOnly(2026, 1, 1),
            EndDate: new DateOnly(2026, 3, 31),
            Status: FiscalPeriodStatus.Open);
        var cash = new AccountResponse(Guid.NewGuid(), "1000", "Cash", AccountType.Asset, period.Id);
        var equity = new AccountResponse(Guid.NewGuid(), "3000", "Owner's Equity", AccountType.Equity, period.Id);

        ctx.Api
            .GetFiscalPeriodsAsync(Arg.Any<CancellationToken>())
            .Returns([period]);
        ctx.Api
            .GetJournalEntriesAsync(Arg.Any<Guid?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<JournalEntryResponse>(entries ?? [], TotalCount: entries?.Count ?? 0, Page: 1, PageSize: 10));
        ctx.Api
            .GetAccountsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns([cash, equity]);
        ctx.Api
            .CreateJournalEntryAsync(Arg.Any<CreateJournalEntryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CreatedResponse(Guid.NewGuid()));

        var cut = ctx.RenderComponent<Journal>();
        return new JournalFixture(ctx, cut, period, cash, equity);
    }

    private static IElement PostButton(IRenderedComponent<Journal> cut) =>
        cut.FindAll("button").ToArray().Single(b => b.TextContent.Contains("Post entry"));

    private static IElement[] AmountInputs(IRenderedComponent<Journal> cut) =>
        cut.FindAll("input[placeholder='0.00']").ToArray();

    private static IElement[] AccountSelects(IRenderedComponent<Journal> cut) =>
        cut.FindAll("select").ToArray();

    private static async Task OpenPostModal(IRenderedComponent<Journal> cut)
    {
        await PostButton(cut).ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() => Assert.Contains("Post journal entry", cut.Markup));
    }

    private static async Task FillBalancedLines(JournalFixture fixture, string amount)
    {
        var cut = fixture.Cut;
        await AccountSelects(cut)[0].ChangeAsync(new ChangeEventArgs { Value = fixture.Cash.Id.ToString() });
        await AmountInputs(cut)[0].InputAsync(new ChangeEventArgs { Value = amount });
        await AccountSelects(cut)[1].ChangeAsync(new ChangeEventArgs { Value = fixture.Equity.Id.ToString() });
        await AmountInputs(cut)[3].InputAsync(new ChangeEventArgs { Value = amount });
        cut.WaitForAssertion(() => Assert.Contains("Balanced", cut.Markup));
    }

    [Fact]
    public async Task Post_button_in_modal_stays_disabled_until_lines_balance()
    {
        using var fixture = RenderJournal();
        var cut = fixture.Cut;

        cut.WaitForAssertion(() => Assert.Contains("No journal entries yet", cut.Markup));
        await OpenPostModal(cut);

        // Fresh modal: nothing entered, awaiting amounts, post disabled.
        Assert.Contains("Awaiting amounts", cut.Markup);
        Assert.Contains(cut.FindAll("button[disabled]"), b => b.TextContent.Contains("Post entry"));

        // Fill line 1 only: Cash debit 100 — still unbalanced, post stays disabled.
        await AccountSelects(cut)[0].ChangeAsync(new ChangeEventArgs { Value = fixture.Cash.Id.ToString() });
        await AmountInputs(cut)[0].InputAsync(new ChangeEventArgs { Value = "100" });

        cut.WaitForAssertion(() => Assert.Contains("Out of balance by $100.00", cut.Markup));
        Assert.Contains(cut.FindAll("button[disabled]"), b => b.TextContent.Contains("Post entry"));

        // Fill line 2: Equity credit 100 — balanced, post enabled.
        await AccountSelects(cut)[1].ChangeAsync(new ChangeEventArgs { Value = fixture.Equity.Id.ToString() });
        await AmountInputs(cut)[3].InputAsync(new ChangeEventArgs { Value = "100" });

        cut.WaitForAssertion(() => Assert.Contains("Balanced", cut.Markup));
        Assert.Contains("Ready to post.", cut.Markup);
        Assert.DoesNotContain(cut.FindAll("button[disabled]"), b => b.TextContent.Contains("Post entry"));
    }

    [Fact]
    public async Task Posting_balanced_entry_sends_request_closes_modal_and_toasts()
    {
        using var fixture = RenderJournal();
        var cut = fixture.Cut;

        cut.WaitForAssertion(() => Assert.Contains("No journal entries yet", cut.Markup));
        await OpenPostModal(cut);
        await FillBalancedLines(fixture, "250.50");

        // The toolbar also has a "Post entry" button (it opens the modal), so scope the
        // click to the modal's footer button inside the dialog.
        await cut.Find("div[role='dialog']").QuerySelectorAll("button")
            .Single(b => b.TextContent.Contains("Post entry"))
            .ClickAsync(new MouseEventArgs());

        await fixture.Context.Api.Received(1).CreateJournalEntryAsync(
            Arg.Is<CreateJournalEntryRequest>(request =>
                request.PeriodId == fixture.Period.Id
                && request.Lines.Count == 2
                && request.Lines[0].AccountId == fixture.Cash.Id
                && request.Lines[0].Debit == 250.50m
                && request.Lines[0].Credit == 0m
                && request.Lines[1].AccountId == fixture.Equity.Id
                && request.Lines[1].Debit == 0m
                && request.Lines[1].Credit == 250.50m),
            Arg.Any<CancellationToken>());

        cut.WaitForAssertion(() => Assert.DoesNotContain("Post journal entry", cut.Markup));
        Assert.Equal("Journal entry posted.", Assert.Single(fixture.Context.Toast.GetToasts()).Message);
    }

    [Fact]
    public async Task Unbalanced_amounts_show_out_of_balance_pill_and_block_post()
    {
        using var fixture = RenderJournal();
        var cut = fixture.Cut;

        cut.WaitForAssertion(() => Assert.Contains("No journal entries yet", cut.Markup));
        await OpenPostModal(cut);

        await AccountSelects(cut)[0].ChangeAsync(new ChangeEventArgs { Value = fixture.Cash.Id.ToString() });
        await AmountInputs(cut)[0].InputAsync(new ChangeEventArgs { Value = "100" });
        await AccountSelects(cut)[1].ChangeAsync(new ChangeEventArgs { Value = fixture.Equity.Id.ToString() });
        await AmountInputs(cut)[3].InputAsync(new ChangeEventArgs { Value = "90" });

        cut.WaitForAssertion(() => Assert.Contains("Out of balance by $10.00", cut.Markup));
        Assert.Contains(cut.FindAll("button[disabled]"), b => b.TextContent.Contains("Post entry"));
        Assert.DoesNotContain("Balanced", cut.Markup);

        await fixture.Context.Api.DidNotReceiveWithAnyArgs().CreateJournalEntryAsync(default!, default);
    }

    [Fact]
    public void Renders_period_name_and_existing_entries()
    {
        var line = new JournalEntryLine(Guid.NewGuid(), 120m, 0m);
        var entry = new JournalEntryResponse(
            Id: Guid.NewGuid(),
            FiscalPeriodId: Guid.NewGuid(),
            Description: "Opening deposit",
            OccurredOnUtc: new DateTime(2026, 8, 1, 10, 30, 0, DateTimeKind.Utc),
            IsPosted: true,
            Lines: [line]);

        using var fixture = RenderJournal([entry]);
        var cut = fixture.Cut;

        cut.WaitForAssertion(() => Assert.Contains("Opening deposit", cut.Markup));
        Assert.Contains("Posted entries · 2026 Q1", cut.Markup);
        Assert.Contains("$120.00", cut.Markup);
        Assert.Contains("1 lines", cut.Markup);
    }
}
