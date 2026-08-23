using Bunit;
using LedgerLite.Web.Client.Ui;

namespace LedgerLite.Web.Tests.Components;

public sealed class DataTableTests : TestContext
{
    private static readonly string[] Items = ["Alpha", "Beta", "Gamma"];

    private IRenderedComponent<DataTable<string>> RenderTable(
        IEnumerable<string>? items,
        RenderFragment? footer = null)
    {
        return RenderComponent<DataTable<string>>(parameters => parameters
            .Add(p => p.Items, items)
            .Add(p => p.TableHeader, header => header.AddMarkupContent(0, "<th>Name</th>"))
            .Add(p => p.Row, (RenderFragment<string>)(item => builder => builder.AddMarkupContent(0, $"<td>{item}</td>")))
            .Add(p => p.TableFooter, footer));
    }

    [Fact]
    public void Renders_header_and_one_row_per_item()
    {
        var cut = RenderTable(Items);

        var header = cut.FindAll("thead th").Single();
        Assert.Equal("Name", header.TextContent.Trim());

        var rows = cut.FindAll("tbody tr");
        Assert.Equal(3, rows.Count);
        Assert.Contains("Alpha", cut.Markup);
        Assert.Contains("Gamma", cut.Markup);
    }

    [Fact]
    public void Empty_items_renders_nothing()
    {
        var cut = RenderTable([]);

        Assert.Empty(cut.FindAll("table"));
        Assert.Empty(cut.FindAll("td"));
    }

    [Fact]
    public void Null_items_renders_nothing()
    {
        var cut = RenderTable(null);

        Assert.Empty(cut.FindAll("table"));
    }

    [Fact]
    public void Footer_renders_when_supplied()
    {
        RenderFragment footer = builder => builder.AddMarkupContent(0, "<tr><td>TOTAL</td></tr>");
        var cut = RenderTable(Items, footer);

        Assert.Contains("TOTAL", cut.Find("tfoot").TextContent);
    }

    [Fact]
    public void Footer_absent_renders_no_tfoot()
    {
        var cut = RenderTable(Items);

        Assert.Empty(cut.FindAll("tfoot"));
    }
}
