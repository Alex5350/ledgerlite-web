using Bunit;
using LedgerLite.Web.Client.Pages;

namespace LedgerLite.Web.Tests.Components;

public sealed class NotFoundPageTests : TestContext
{
    [Fact]
    public void Renders_404_copy()
    {
        var cut = RenderComponent<NotFound>();

        Assert.Contains("404", cut.Markup);
        Assert.Contains("This page doesn't balance", cut.Markup);
        Assert.Contains("was moved, closed, or never existed", cut.Markup);
        Assert.Contains("Back to overview", cut.Markup);
    }

    [Fact]
    public void Renders_link_back_to_overview()
    {
        var cut = RenderComponent<NotFound>();

        var link = cut.Find("a[href='/']");
        Assert.Equal("Back to overview", link.TextContent.Trim());
    }
}
