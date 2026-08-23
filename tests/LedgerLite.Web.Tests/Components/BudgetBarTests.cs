using Bunit;
using LedgerLite.Web.Client.Ui;

namespace LedgerLite.Web.Tests.Components;

public sealed class BudgetBarTests : TestContext
{
    private IRenderedComponent<BudgetBar> RenderBar(decimal spent, decimal limit, string label = "Groceries") =>
        RenderComponent<BudgetBar>(parameters => parameters
            .Add(p => p.Label, label)
            .Add(p => p.Spent, spent)
            .Add(p => p.Limit, limit));

    private IElement FillBar(IRenderedComponent<BudgetBar> cut) => cut.Find("div[role='progressbar'] > div");

    [Fact]
    public void Under_eighty_percent_renders_emerald_bar_and_percent_text()
    {
        var cut = RenderBar(spent: 50m, limit: 100m);

        var bar = FillBar(cut);
        Assert.Contains("bg-accent-500", bar.GetAttribute("class"));
        Assert.DoesNotContain("bg-warn-400", bar.GetAttribute("class"));
        Assert.Contains("50% used", cut.Markup);
        Assert.Equal(50m, decimal.Parse(cut.Find("div[role='progressbar']").GetAttribute("aria-valuenow")!));
    }

    [Fact]
    public void At_eighty_percent_renders_amber_bar()
    {
        var cut = RenderBar(spent: 80m, limit: 100m);

        var bar = FillBar(cut);
        Assert.Contains("bg-warn-400", bar.GetAttribute("class"));
        Assert.DoesNotContain("bg-accent-500", bar.GetAttribute("class"));
        Assert.Contains("80% used", cut.Markup);
    }

    [Fact]
    public void Over_eighty_percent_renders_amber_bar()
    {
        var cut = RenderBar(spent: 95m, limit: 100m);

        Assert.Contains("bg-warn-400", FillBar(cut).GetAttribute("class"));
        Assert.Contains("95% used", cut.Markup);
    }

    [Fact]
    public void At_one_hundred_percent_renders_red_bar()
    {
        var cut = RenderBar(spent: 100m, limit: 100m);

        var bar = FillBar(cut);
        Assert.Contains("bg-danger-500", bar.GetAttribute("class"));
        Assert.Contains("100% used", cut.Markup);
        Assert.Contains("text-danger-400", cut.Markup);
    }

    [Fact]
    public void Over_one_hundred_percent_shows_true_percent_but_clamped_width()
    {
        var cut = RenderBar(spent: 150m, limit: 100m);

        var bar = FillBar(cut);
        Assert.Contains("bg-danger-500", bar.GetAttribute("class"));
        Assert.Contains("150% used", cut.Markup);
        Assert.Contains("width:100%", bar.GetAttribute("style"));
    }

    [Fact]
    public void Zero_limit_renders_zero_percent_safely()
    {
        var cut = RenderBar(spent: 0m, limit: 0m);

        Assert.Contains("0% used", cut.Markup);
        Assert.Contains("bg-accent-500", FillBar(cut).GetAttribute("class"));
    }

    [Fact]
    public void Renders_label_and_amounts()
    {
        var cut = RenderBar(spent: 25m, limit: 100m, label: "Coffee");

        Assert.Contains("Coffee", cut.Markup);
        Assert.Contains("$25.00 / $100.00", cut.Markup);
    }
}
