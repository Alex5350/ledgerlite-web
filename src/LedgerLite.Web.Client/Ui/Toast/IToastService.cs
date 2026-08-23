namespace LedgerLite.Web.Client.Ui;

/// <summary>A single queued toast notification.</summary>
/// <param name="Id">Stable id used for dismissal.</param>
/// <param name="Tone">Visual tone of the toast.</param>
/// <param name="Message">Human-readable message.</param>
public sealed record ToastMessage(string Id, ToastTone Tone, string Message);

/// <summary>
/// App-wide toast notifications. Toasts auto-dismiss after 4 seconds and are
/// rendered by <see cref="Toasts"/> (mounted once in Routes.razor).
/// </summary>
public interface IToastService
{
    /// <summary>Raised whenever the toast queue changes so the container can re-render.</summary>
    event Action? Changed;

    /// <summary>Queues a success toast (emerald).</summary>
    void ShowSuccess(string message);

    /// <summary>Queues an error toast (red).</summary>
    void ShowError(string message);

    /// <summary>Queues an informational toast (sky).</summary>
    void ShowInfo(string message);

    /// <summary>Removes a toast immediately (used by the container's X button).</summary>
    void Dismiss(string toastId);

    /// <summary>Snapshot of the currently visible toasts.</summary>
    IReadOnlyList<ToastMessage> GetToasts();
}

/// <summary>
/// Default <see cref="IToastService"/>: an in-memory queue with 4-second auto-dismiss.
/// State mutations are lock-guarded so the service is safe to mock or call from any thread.
/// </summary>
public sealed class ToastService : IToastService
{
    private static readonly TimeSpan AutoDismissAfter = TimeSpan.FromSeconds(4);

    private readonly object _gate = new();
    private readonly List<ToastMessage> _toasts = [];

    public event Action? Changed;

    public void ShowSuccess(string message) => Show(ToastTone.Success, message);

    public void ShowError(string message) => Show(ToastTone.Error, message);

    public void ShowInfo(string message) => Show(ToastTone.Info, message);

    public void Dismiss(string toastId)
    {
        lock (_gate)
        {
            if (_toasts.RemoveAll(toast => toast.Id == toastId) == 0)
            {
                return;
            }
        }

        Changed?.Invoke();
    }

    public IReadOnlyList<ToastMessage> GetToasts()
    {
        lock (_gate)
        {
            return _toasts.ToArray();
        }
    }

    private void Show(ToastTone tone, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var toast = new ToastMessage(Id: Guid.NewGuid().ToString("N"), tone, message);
        lock (_gate)
        {
            _toasts.Add(toast);
        }

        Changed?.Invoke();
        _ = AutoDismissAsync(toast.Id);
    }

    private async Task AutoDismissAsync(string toastId)
    {
        try
        {
            await Task.Delay(AutoDismissAfter);
        }
        catch (TaskCanceledException)
        {
            // Host shutting down: nothing to clean up.
        }

        Dismiss(toastId);
    }
}
