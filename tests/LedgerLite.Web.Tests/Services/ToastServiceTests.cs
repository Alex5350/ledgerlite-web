using LedgerLite.Web.Client.Ui;

namespace LedgerLite.Web.Tests.Services;

public sealed class ToastServiceTests
{
    [Fact]
    public void ShowSuccess_enqueues_success_toast_and_raises_Changed()
    {
        var service = new ToastService();
        var changed = 0;
        service.Changed += () => changed++;

        service.ShowSuccess("Saved.");

        Assert.Equal(1, changed);
        var toast = Assert.Single(service.GetToasts());
        Assert.Equal(ToastTone.Success, toast.Tone);
        Assert.Equal("Saved.", toast.Message);
        Assert.False(string.IsNullOrWhiteSpace(toast.Id));
    }

    [Fact]
    public void ShowError_enqueues_error_toast()
    {
        var service = new ToastService();

        service.ShowError("It broke.");

        var toast = Assert.Single(service.GetToasts());
        Assert.Equal(ToastTone.Error, toast.Tone);
        Assert.Equal("It broke.", toast.Message);
    }

    [Fact]
    public void ShowInfo_enqueues_info_toast()
    {
        var service = new ToastService();

        service.ShowInfo("Heads up.");

        var toast = Assert.Single(service.GetToasts());
        Assert.Equal(ToastTone.Info, toast.Tone);
    }

    [Fact]
    public void Whitespace_message_is_ignored()
    {
        var service = new ToastService();
        var changed = 0;
        service.Changed += () => changed++;

        service.ShowSuccess("   ");

        Assert.Empty(service.GetToasts());
        Assert.Equal(0, changed);
    }

    [Fact]
    public void Multiple_toasts_queue_in_order()
    {
        var service = new ToastService();

        service.ShowSuccess("first");
        service.ShowError("second");
        service.ShowInfo("third");

        Assert.Equal(["first", "second", "third"], service.GetToasts().Select(t => t.Message).ToArray());
    }

    [Fact]
    public void Dismiss_removes_toast_and_raises_Changed()
    {
        var service = new ToastService();
        service.ShowInfo("temporary");
        var toast = Assert.Single(service.GetToasts());
        var changed = 0;
        service.Changed += () => changed++;

        service.Dismiss(toast.Id);

        Assert.Equal(1, changed);
        Assert.Empty(service.GetToasts());
    }

    [Fact]
    public void Dismiss_with_unknown_id_is_a_no_op()
    {
        var service = new ToastService();
        service.ShowInfo("stays");
        var changed = 0;
        service.Changed += () => changed++;

        service.Dismiss("does-not-exist");

        Assert.Equal(0, changed);
        Assert.Single(service.GetToasts());
    }

    [Fact]
    public async Task Toast_auto_dismisses_after_four_seconds()
    {
        var service = new ToastService();
        service.ShowSuccess("fleeting");

        Assert.Single(service.GetToasts());
        await Task.Delay(TimeSpan.FromSeconds(4.5));

        Assert.Empty(service.GetToasts());
    }

    [Fact]
    public async Task GetToasts_snapshot_is_isolated_from_later_mutations()
    {
        var service = new ToastService();
        service.ShowInfo("original");
        var snapshot = service.GetToasts();

        service.ShowInfo("added later");

        Assert.Single(snapshot);
        Assert.Equal(2, service.GetToasts().Count);
    }
}
