using LedgerLite.Web.Client.Services.Api;
using LedgerLite.Web.Client.Ui;
using Microsoft.JSInterop;

namespace LedgerLite.Web.Tests.Services;

public sealed class PeriodStateTests
{
    private static FiscalPeriodResponse MakePeriod(FiscalPeriodStatus status = FiscalPeriodStatus.Open, string name = "2026") =>
        new(Guid.NewGuid(), name, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), status);

    private sealed record Setup(ILedgerLiteApiClient Api, IJSRuntime Js, PeriodState State);

    /// <summary>
    /// Hand-rolled JS runtime spy: InvokeVoidAsync's underlying InvokeAsync&lt;IJSVoidResult&gt;
    /// generic argument is not referenceable, so a plain double is easier than NSubstitute here.
    /// </summary>
    private sealed class SpyJsRuntime(Action<string, object?[]?>? onInvoke = null) : IJSRuntime
    {
        public List<(string Identifier, object?[]? Args)> Calls { get; } = [];

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            Record(identifier, args);
            return default;
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            Record(identifier, args);
            return default;
        }

        private void Record(string identifier, object?[]? args)
        {
            Calls.Add((identifier, args));
            onInvoke?.Invoke(identifier, args);
        }
    }

    private static Setup Create(
        IReadOnlyList<FiscalPeriodResponse>? periods = null,
        string? storedPeriodId = null)
    {
        var api = Substitute.For<ILedgerLiteApiClient>();
        var js = Substitute.For<IJSRuntime>();

        api
            .GetFiscalPeriodsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<FiscalPeriodResponse>>(periods ?? []));

        // An unconfigured substitute call returns a completed ValueTask<string> whose
        // result is null — exactly what "nothing stored" looks like to PeriodState.
        if (storedPeriodId is { } id)
        {
            js
                .InvokeAsync<string>("localStorage.getItem", Arg.Any<CancellationToken>(), Arg.Any<object[]>())
                .Returns(new ValueTask<string>(id));
        }

        return new Setup(api, js, new PeriodState(api, js));
    }

    [Fact]
    public async Task LoadAsync_picks_stored_period_when_id_exists_in_list()
    {
        var openA = MakePeriod(name: "Open A");
        var closedB = MakePeriod(FiscalPeriodStatus.Closed, "Closed B");
        var openC = MakePeriod(name: "Open C");
        var setup = Create([openA, closedB, openC], storedPeriodId: closedB.Id.ToString());

        await setup.State.LoadAsync();

        Assert.Equal(closedB, setup.State.CurrentPeriod);
        Assert.Equal([openA, closedB, openC], setup.State.Periods);
        Assert.True(setup.State.IsLoaded);
    }

    [Fact]
    public async Task LoadAsync_falls_back_to_first_open_period_when_nothing_stored()
    {
        var closedA = MakePeriod(FiscalPeriodStatus.Closed, "Closed A");
        var openB = MakePeriod(name: "Open B");
        var openC = MakePeriod(name: "Open C");
        var setup = Create([closedA, openB, openC], storedPeriodId: null);

        await setup.State.LoadAsync();

        Assert.Equal(openB, setup.State.CurrentPeriod);
    }

    [Fact]
    public async Task LoadAsync_falls_back_to_first_open_period_when_stored_id_unknown()
    {
        var openA = MakePeriod(name: "Open A");
        var openB = MakePeriod(name: "Open B");
        var setup = Create([openA, openB], storedPeriodId: Guid.NewGuid().ToString());

        await setup.State.LoadAsync();

        Assert.Equal(openA, setup.State.CurrentPeriod);
    }

    [Fact]
    public async Task LoadAsync_falls_back_to_first_period_when_none_are_open()
    {
        var closedA = MakePeriod(FiscalPeriodStatus.Closed, "Closed A");
        var closedB = MakePeriod(FiscalPeriodStatus.Closed, "Closed B");
        var setup = Create([closedA, closedB], storedPeriodId: null);

        await setup.State.LoadAsync();

        Assert.Equal(closedA, setup.State.CurrentPeriod);
    }

    [Fact]
    public async Task SetAsync_raises_Changed_and_persists_selection()
    {
        var period = MakePeriod();
        var js = new SpyJsRuntime();
        var state = new PeriodState(Substitute.For<ILedgerLiteApiClient>(), js);

        var raised = 0;
        state.Changed += () => raised++;

        await state.SetAsync(period);

        Assert.Equal(1, raised);
        Assert.Equal(period, state.CurrentPeriod);
        Assert.Contains(period, state.Periods);

        var call = Assert.Single(js.Calls, c => c.Identifier == "localStorage.setItem");
        Assert.Equal(["ledgerlite.period", period.Id.ToString()], call.Args);
    }

    [Fact]
    public async Task SetAsync_swallows_prerender_js_failures()
    {
        var period = MakePeriod();
        var js = new SpyJsRuntime((_, _) => throw new JSDisconnectedException("The circuit is not initialized."));
        var state = new PeriodState(Substitute.For<ILedgerLiteApiClient>(), js);

        var raised = 0;
        state.Changed += () => raised++;

        // Must not throw even though local storage is unreachable (static prerender).
        await state.SetAsync(period);

        Assert.Equal(1, raised);
        Assert.Equal(period, state.CurrentPeriod);
    }

    [Fact]
    public async Task LoadAsync_shares_single_in_flight_request_between_callers()
    {
        var api = Substitute.For<ILedgerLiteApiClient>();
        var js = Substitute.For<IJSRuntime>();
        var source = new TaskCompletionSource<IReadOnlyList<FiscalPeriodResponse>>();
        api
            .GetFiscalPeriodsAsync(Arg.Any<CancellationToken>())
            .Returns(source.Task);

        var state = new PeriodState(api, js);
        var first = state.LoadAsync();
        var second = state.LoadAsync();

        source.SetResult([MakePeriod()]);
        await first;
        await second;

        await api.Received(1).GetFiscalPeriodsAsync(Arg.Any<CancellationToken>());
        Assert.True(state.IsLoaded);
    }

    [Fact]
    public async Task LoadAsync_failure_resets_cache_so_next_call_can_retry()
    {
        var api = Substitute.For<ILedgerLiteApiClient>();
        var js = Substitute.For<IJSRuntime>();
        var period = MakePeriod();
        var attempts = 0;
        api
            .GetFiscalPeriodsAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                if (++attempts == 1)
                {
                    throw new ApiException(500, "Api.Down");
                }

                return Task.FromResult<IReadOnlyList<FiscalPeriodResponse>>([period]);
            });

        var state = new PeriodState(api, js);

        await Assert.ThrowsAsync<ApiException>(() => state.LoadAsync());
        Assert.False(state.IsLoaded);

        await state.LoadAsync();

        Assert.True(state.IsLoaded);
        Assert.Equal(period, state.CurrentPeriod);
    }
}
