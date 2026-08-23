using Bunit;
using LedgerLite.Web.Client.Ui;

namespace LedgerLite.Web.Tests.Components;

public sealed class FieldTests : TestContext
{
    [Fact]
    public void Error_renders_alert_text_and_invalid_input()
    {
        var cut = RenderComponent<Field>(parameters => parameters
            .Add(p => p.Label, "Email")
            .Add(p => p.Error, "Email is required"));

        var alert = cut.Find("p[role='alert']");
        Assert.Equal("Email is required", alert.TextContent.Trim());
        Assert.Equal("true", cut.Find("input").GetAttribute("aria-invalid"));
        Assert.Contains("border-danger-500/60", cut.Find("input").GetAttribute("class"));
    }

    [Fact]
    public void Hint_renders_when_no_error_and_hides_when_error_set()
    {
        var cut = RenderComponent<Field>(parameters => parameters
            .Add(p => p.Label, "Amount")
            .Add(p => p.Hint, "Plain numbers only"));

        Assert.Contains("Plain numbers only", cut.Markup);
        Assert.Empty(cut.FindAll("p[role='alert']"));

        cut.SetParametersAndRender(p => p.Add(x => x.Error, "Bad amount"));

        Assert.Contains("Bad amount", cut.Markup);
        Assert.DoesNotContain("Plain numbers only", cut.Markup);
    }

    [Fact]
    public async Task Input_raises_ValueChanged_with_typed_value()
    {
        string? captured = null;
        var cut = RenderComponent<Field>(parameters => parameters
            .Add(p => p.Label, "Email")
            .Add(p => p.Value, string.Empty)
            .Add(p => p.ValueChanged, EventCallback.Factory.Create<string>(this, value => captured = value)));

        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = "user@example.io" });

        Assert.Equal("user@example.io", captured);
    }

    [Fact]
    public async Task Input_with_empty_text_raises_empty_string()
    {
        string? captured = null;
        var cut = RenderComponent<Field>(parameters => parameters
            .Add(p => p.Value, "old")
            .Add(p => p.ValueChanged, EventCallback.Factory.Create<string>(this, value => captured = value)));

        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = string.Empty });

        Assert.Equal(string.Empty, captured);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Mono_appends_font_class(bool mono)
    {
        var cut = RenderComponent<Field>(parameters => parameters
            .Add(p => p.Mono, mono));

        var inputClass = cut.Find("input").GetAttribute("class");
        if (mono)
        {
            Assert.Contains("font-mono", inputClass);
        }
        else
        {
            Assert.DoesNotContain("font-mono", inputClass);
        }
    }

    [Fact]
    public void Label_and_placeholder_render()
    {
        var cut = RenderComponent<Field>(parameters => parameters
            .Add(p => p.Label, "Description")
            .Add(p => p.Placeholder, "e.g. Grocery run"));

        Assert.Contains("Description", cut.Markup);
        Assert.Equal("e.g. Grocery run", cut.Find("input").GetAttribute("placeholder"));
    }
}
