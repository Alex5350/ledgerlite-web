using Bunit;
using LedgerLite.Web.Client.Ui;

namespace LedgerLite.Web.Tests.Components;

public sealed class ModalTests : TestContext
{
    private static RenderFragment Body(string text) => builder => builder.AddContent(0, text);

    private IRenderedComponent<Modal> RenderModal(
        bool isOpen,
        string title = "Post journal entry",
        string? subtitle = null)
    {
        return RenderComponent<Modal>(parameters => parameters
            .Add(p => p.IsOpen, isOpen)
            .Add(p => p.Title, title)
            .Add(p => p.Subtitle, subtitle)
            .Add(p => p.ChildContent, Body("Modal body content")));
    }

    [Fact]
    public void Open_renders_dialog_title_and_content()
    {
        var cut = RenderModal(isOpen: true, subtitle: "Debits must equal credits");

        var dialog = cut.Find("[role='dialog']");
        Assert.Equal("Post journal entry", dialog.GetAttribute("aria-label"));
        Assert.Contains("Post journal entry", cut.Markup);
        Assert.Contains("Debits must equal credits", cut.Markup);
        Assert.Contains("Modal body content", cut.Markup);
        Assert.Equal("true", dialog.GetAttribute("aria-modal"));
    }

    [Fact]
    public void Closed_renders_nothing()
    {
        var cut = RenderModal(isOpen: false);

        Assert.Empty(cut.FindAll("[role='dialog']"));
        Assert.DoesNotContain("Post journal entry", cut.Markup);
        Assert.DoesNotContain("Modal body content", cut.Markup);
    }

    [Fact]
    public async Task Close_button_invokes_IsOpenChanged_with_false()
    {
        bool? received = null;
        var cut = RenderComponent<Modal>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.Title, "Dialog")
            .Add(p => p.ChildContent, Body("body"))
            .Add(p => p.IsOpenChanged, EventCallback.Factory.Create<bool>(this, value => received = value)));

        await cut.Find("button[aria-label='Close dialog']").ClickAsync(new MouseEventArgs());

        Assert.False(received);
    }

    [Fact]
    public async Task Escape_key_invokes_IsOpenChanged_with_false()
    {
        bool? received = null;
        var cut = RenderComponent<Modal>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.Title, "Dialog")
            .Add(p => p.ChildContent, Body("body"))
            .Add(p => p.IsOpenChanged, EventCallback.Factory.Create<bool>(this, value => received = value)));

        // The focusable panel listens for keydown; Escape must request a close.
        var panel = cut.Find("[role='dialog'] div[tabindex='-1']");
        await panel.KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });

        Assert.False(received);
    }

    [Fact]
    public void FooterTemplate_renders_when_supplied()
    {
        RenderFragment footer = builder => builder.AddContent(0, "FOOTER");
        var cut = RenderComponent<Modal>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.Title, "Dialog")
            .Add(p => p.ChildContent, Body("body"))
            .Add(p => p.FooterTemplate, footer));

        Assert.Contains("FOOTER", cut.Markup);
    }
}
