using Bunit;
using LedgerLite.Web.Client.Ui;

namespace LedgerLite.Web.Tests.Components;

public sealed class EmptyStateTests : TestContext
{
    [Fact]
    public void Renders_title_and_hint()
    {
        var cut = RenderComponent<EmptyState>(parameters => parameters
            .Add(p => p.Title, "No accounts yet")
            .Add(p => p.Hint, "Create your first account to get started."));

        Assert.Contains("No accounts yet", cut.Markup);
        Assert.Contains("Create your first account to get started.", cut.Markup);
    }

    [Fact]
    public void Hint_absent_renders_only_title()
    {
        var cut = RenderComponent<EmptyState>(parameters => parameters
            .Add(p => p.Title, "Nothing here"));

        Assert.Contains("Nothing here", cut.Markup);
        Assert.DoesNotContain("leading-relaxed text-slate-500", cut.Markup);
    }

    [Fact]
    public void ActionFragment_renders_when_supplied()
    {
        RenderFragment action = builder => builder.AddContent(0, "ACTION");
        var cut = RenderComponent<EmptyState>(parameters => parameters
            .Add(p => p.Title, "Empty")
            .Add(p => p.ActionFragment, action));

        Assert.Contains("ACTION", cut.Markup);
    }

    [Fact]
    public void IconFragment_replaces_default_glyph()
    {
        RenderFragment icon = builder => builder.AddContent(0, "ICON");
        var cut = RenderComponent<EmptyState>(parameters => parameters
            .Add(p => p.Title, "Empty")
            .Add(p => p.IconFragment, icon));

        Assert.Contains("ICON", cut.Markup);
        Assert.Empty(cut.FindAll("svg"));
    }
}
