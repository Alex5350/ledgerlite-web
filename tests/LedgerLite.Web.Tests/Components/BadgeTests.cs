using Bunit;
using LedgerLite.Web.Client.Ui;

namespace LedgerLite.Web.Tests.Components;

public sealed class BadgeTests : TestContext
{
    [Theory]
    [InlineData(BadgeTone.Slate, "bg-white/5")]
    [InlineData(BadgeTone.Emerald, "border-accent-500/30")]
    [InlineData(BadgeTone.Amber, "border-warn-400/30")]
    [InlineData(BadgeTone.Red, "border-danger-400/30")]
    [InlineData(BadgeTone.Sky, "border-info-400/30")]
    [InlineData(BadgeTone.Violet, "border-violet-400/30")]
    public void Tone_renders_expected_class(BadgeTone tone, string expectedClass)
    {
        var cut = RenderComponent<Badge>(parameters => parameters
            .Add(p => p.Tone, tone)
            .Add(p => p.ChildContent, "Open"));

        var badge = cut.Find("span");
        Assert.Equal("Open", badge.TextContent.Trim());
        Assert.Contains(expectedClass, badge.GetAttribute("class"));
    }

    [Fact]
    public void Tone_defaults_to_slate()
    {
        var cut = RenderComponent<Badge>(parameters => parameters
            .Add(p => p.ChildContent, "fallback"));

        Assert.Contains("bg-white/5", cut.Find("span").GetAttribute("class"));
        Assert.DoesNotContain("border-danger-400/30", cut.Find("span").GetAttribute("class"));
    }
}
