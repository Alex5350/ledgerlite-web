using Bunit;
using LedgerLite.Web.Client.Ui;
using LedgerLite.Web.Tests.Infrastructure;

namespace LedgerLite.Web.Tests.Components;

public sealed class ToastsTests
{
    [Fact]
    public void Toast_shown_before_render_appears_in_container()
    {
        using var ctx = new AppTestContext();

        ctx.Toast.ShowError("Something broke");

        var cut = ctx.RenderComponent<Toasts>();
        cut.WaitForAssertion(() => Assert.Contains("Something broke", cut.Markup));

        // Error tone styling on the icon column.
        Assert.Contains("text-danger-400", cut.Markup);
        Assert.Contains("border-danger-400/30", cut.Markup);
    }

    [Fact]
    public void Changed_event_triggers_re_render_of_mounted_container()
    {
        using var ctx = new AppTestContext();

        var cut = ctx.RenderComponent<Toasts>();
        Assert.DoesNotContain("Files exported", cut.Markup);

        ctx.Toast.ShowSuccess("Files exported");

        cut.WaitForAssertion(() => Assert.Contains("Files exported", cut.Markup));
        Assert.Contains("text-accent-400", cut.Markup);
    }

    [Fact]
    public void Multiple_toasts_all_render()
    {
        using var ctx = new AppTestContext();

        ctx.Toast.ShowInfo("one");
        ctx.Toast.ShowInfo("two");

        var cut = ctx.RenderComponent<Toasts>();
        cut.WaitForAssertion(() => Assert.Contains("two", cut.Markup));

        Assert.Contains("one", cut.Markup);
        Assert.Equal(2, cut.FindAll("button[aria-label='Dismiss notification']").Count);
    }

    [Fact]
    public async Task Dismiss_button_removes_toast()
    {
        using var ctx = new AppTestContext();

        ctx.Toast.ShowError("boom");
        var cut = ctx.RenderComponent<Toasts>();
        cut.WaitForAssertion(() => Assert.Contains("boom", cut.Markup));

        await cut.Find("button[aria-label='Dismiss notification']").ClickAsync(new MouseEventArgs());

        cut.WaitForAssertion(() => Assert.DoesNotContain("boom", cut.Markup));
    }

    [Fact]
    public void Empty_queue_renders_nothing()
    {
        using var ctx = new AppTestContext();

        var cut = ctx.RenderComponent<Toasts>();

        Assert.DoesNotContain("aria-live", cut.Markup);
    }
}
