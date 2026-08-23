using Bunit;
using LedgerLite.Web.Client.Ui;

namespace LedgerLite.Web.Tests.Components;

public sealed class StatCardTests : TestContext
{
    private IRenderedComponent<StatCard> RenderCard(
        string label = "Total debits",
        string value = "$1,234.56",
        StatTone tone = StatTone.Neutral,
        string? delta = null,
        StatTone deltaTone = StatTone.Neutral,
        RenderFragment? icon = null)
    {
        return RenderComponent<StatCard>(parameters => parameters
            .Add(p => p.Label, label)
            .Add(p => p.Value, value)
            .Add(p => p.Tone, tone)
            .Add(p => p.Delta, delta)
            .Add(p => p.DeltaTone, deltaTone)
            .Add(p => p.IconFragment, icon));
    }

    [Fact]
    public void Renders_label_and_value()
    {
        var cut = RenderCard(label: "Net worth", value: "$9,001.00");

        Assert.Contains("Net worth", cut.Markup);
        Assert.Contains("$9,001.00", cut.Markup);
    }

    [Fact]
    public void Value_tone_renders_expected_class()
    {
        var cut = RenderCard(tone: StatTone.Red);

        // The big value paragraph carries the tone class.
        Assert.Contains("text-danger-400", cut.Markup);
    }

    [Fact]
    public void Delta_renders_with_tone_class()
    {
        var cut = RenderCard(delta: "up 5.2%", deltaTone: StatTone.Emerald);

        Assert.Contains("up 5.2%", cut.Markup);
        Assert.Contains("text-accent-400", cut.Markup);
    }

    [Fact]
    public void No_delta_renders_nothing_under_value()
    {
        var cut = RenderCard();

        Assert.DoesNotContain("mt-1.5", cut.Markup);
    }

    [Fact]
    public void IconFragment_renders_when_supplied()
    {
        RenderFragment icon = builder => builder.AddContent(0, "ICON");
        var cut = RenderCard(icon: icon);

        Assert.Contains("ICON", cut.Markup);
    }

    [Fact]
    public void IconFragment_absent_renders_no_icon_container()
    {
        var cut = RenderCard();

        Assert.DoesNotContain("ICON", cut.Markup);
        Assert.DoesNotContain("rounded-lg border border-white/[0.07]", cut.Markup);
    }
}
