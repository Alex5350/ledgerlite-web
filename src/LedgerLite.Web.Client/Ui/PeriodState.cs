using LedgerLite.Web.Client.Services.Api;
using Microsoft.JSInterop;

namespace LedgerLite.Web.Client.Ui;

/// <summary>
/// Circuit-scoped selection state for the "current" fiscal period. Shared by the topbar
/// selector (writes) and every page (reads + <see cref="Changed"/> subscriptions).
///
/// Selection persistence uses local storage ("ledgerlite.period") and is best-effort:
/// during static prerender and after circuit disconnects the JS interop calls fail and
/// are swallowed, so the in-memory state stays authoritative.
/// </summary>
public sealed class PeriodState(ILedgerLiteApiClient apiClient, IJSRuntime jsRuntime)
{
    private const string StorageKey = "ledgerlite.period";

    private readonly object _gate = new();
    private Task? _loadTask;

    /// <summary>All fiscal periods known to the client (refreshed by <see cref="LoadAsync"/>).</summary>
    public IReadOnlyList<FiscalPeriodResponse> Periods { get; private set; } = [];

    /// <summary>The selected period, or null when none exists / nothing selectable.</summary>
    public FiscalPeriodResponse? CurrentPeriod { get; private set; }

    /// <summary>True once <see cref="LoadAsync"/> has completed successfully.</summary>
    public bool IsLoaded { get; private set; }

    /// <summary>
    /// Raised after the selection changed via <see cref="SetAsync"/>.
    /// <see cref="LoadAsync"/> deliberately does NOT raise this event (it is not a user action);
    /// initial data loading is each page's own responsibility.
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// Loads the period list (concurrent callers share one in-flight request) and picks the
    /// current period: previously stored selection, else the first Open period, else the first.
    /// Throws <see cref="ApiException"/> on API failure so callers can render an error panel.
    /// </summary>
    public Task LoadAsync(CancellationToken cancellationToken = default)
    {
        Task load;
        lock (_gate)
        {
            _loadTask ??= LoadInternalAsync(cancellationToken);
            load = _loadTask;
        }

        return load;
    }

    private async Task LoadInternalAsync(CancellationToken cancellationToken)
    {
        try
        {
            var periods = await apiClient.GetFiscalPeriodsAsync(cancellationToken);
            var storedId = await TryReadStoredPeriodIdAsync(cancellationToken);

            Periods = periods;
            CurrentPeriod = periods.FirstOrDefault(period => period.Id == storedId)
                ?? periods.FirstOrDefault(period => period.Status == FiscalPeriodStatus.Open)
                ?? periods.FirstOrDefault();
            IsLoaded = true;
        }
        catch
        {
            // Allow a later LoadAsync call to retry instead of caching the failure forever.
            lock (_gate)
            {
                _loadTask = null;
            }

            throw;
        }
    }

    /// <summary>Selects a period, persists the choice and raises <see cref="Changed"/>.</summary>
    public async Task SetAsync(FiscalPeriodResponse period, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(period);

        CurrentPeriod = period;
        if (Periods.All(existing => existing.Id != period.Id))
        {
            Periods = [.. Periods, period];
        }

        await TryWriteStoredPeriodIdAsync(period.Id, cancellationToken);
        Changed?.Invoke();
    }

    private async Task<Guid?> TryReadStoredPeriodIdAsync(CancellationToken cancellationToken)
    {
        try
        {
            var stored = await jsRuntime.InvokeAsync<string>("localStorage.getItem", cancellationToken, StorageKey);
            return Guid.TryParse(stored, out var id) ? id : null;
        }
        catch (Exception ex) when (ex is InvalidOperationException or JSDisconnectedException)
        {
            // Prerender / disconnected circuit: no stored selection available.
            return null;
        }
    }

    private async Task TryWriteStoredPeriodIdAsync(Guid periodId, CancellationToken cancellationToken)
    {
        try
        {
            await jsRuntime.InvokeVoidAsync("localStorage.setItem", cancellationToken, StorageKey, periodId.ToString());
        }
        catch (Exception ex) when (ex is InvalidOperationException or JSDisconnectedException)
        {
            // Prerender / disconnected circuit: in-memory selection remains in effect.
        }
    }
}
