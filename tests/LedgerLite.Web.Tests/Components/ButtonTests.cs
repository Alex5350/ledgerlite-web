using Bunit;
using LedgerLite.Web.Client.Ui;
using Microsoft.AspNetCore.Components.Web;

namespace LedgerLite.Web.Tests.Components;

public sealed class ButtonTests : TestContext
{
    private static readonly RenderFragment Label = builder => builder.AddContent(0, "Save changes");

    private IRenderedComponent<Button> RenderButton(
        bool disabled = false,
        bool loading = false,
        ButtonVariant variant = ButtonVariant.Primary,
        EventCallback<MouseEventArgs>? onClick = null)
    {
        return RenderComponent<Button>(parameters => parameters
            .Add(p => p.Disabled, disabled)
            .Add(p => p.Loading, loading)
            .Add(p => p.Variant, variant)
            .Add(p => p.OnClick, onClick ?? EventCallback<MouseEventArgs>.Empty)
            .Add(p => p.ChildContent, Label));
    }

    [Fact]
    public void Renders_label_and_is_enabled_by_default()
    {
        var clicked = false;
        var cut = RenderButton(onClick: EventCallback.Factory.Create<MouseEventArgs>(this, () => clicked = true));

        var button = cut.Find("button");
        Assert.Equal("Save changes", button.TextContent.Trim());
        Assert.False(button.HasAttribute("disabled"));

        // An enabled button dispatches its click handler.
        button.Click();
        Assert.True(clicked);
    }

    [Fact]
    public void Disabled_renders_native_disabled_attribute_so_browser_blocks_click()
    {
        var cut = RenderButton(disabled: true);

        // Blocking is native browser behaviour driven by the disabled attribute — the
        // browser never dispatches a click for a disabled button, so OnClick cannot fire.
        // (bUnit dispatches events directly and does not emulate that part.)
        Assert.True(cut.Find("button").HasAttribute("disabled"));
    }

    [Fact]
    public void Loading_disables_button_and_renders_spinner()
    {
        var cut = RenderButton(loading: true);

        var button = cut.Find("button");
        Assert.True(button.HasAttribute("disabled"));

        // Loading forces the disabled state and adds a spinner to the content.
        Assert.NotNull(cut.Find("svg[role='status']"));
        Assert.Equal("Loading", cut.Find("svg[role='status']").GetAttribute("aria-label"));
    }

    [Fact]
    public void Loading_can_be_toggled_on_re_render()
    {
        var cut = RenderButton(loading: false);
        Assert.False(cut.Find("button").HasAttribute("disabled"));

        cut.SetParametersAndRender(p => p.Add(x => x.Loading, true));

        Assert.True(cut.Find("button").HasAttribute("disabled"));
        Assert.NotNull(cut.Find("svg[role='status']"));
    }

    [Theory]
    [InlineData(ButtonVariant.Primary, "bg-accent-500")]
    [InlineData(ButtonVariant.Primary, "shadow-accent-500/25")]
    [InlineData(ButtonVariant.Secondary, "border-white/10")]
    [InlineData(ButtonVariant.Secondary, "hover:bg-white/5")]
    [InlineData(ButtonVariant.Danger, "bg-danger-500")]
    [InlineData(ButtonVariant.Danger, "text-white")]
    public void Variant_renders_expected_class(ButtonVariant variant, string expectedClass)
    {
        var cut = RenderButton(variant: variant);

        Assert.Contains(expectedClass, cut.Find("button").GetAttribute("class"));
    }

    [Fact]
    public void Type_defaults_to_button_and_is_overridable()
    {
        var defaultCut = RenderComponent<Button>(p => p
            .Add(x => x.OnClick, EventCallback<MouseEventArgs>.Empty)
            .Add(x => x.ChildContent, Label));

        var submitCut = RenderComponent<Button>(p => p
            .Add(x => x.Type, "submit")
            .Add(x => x.OnClick, EventCallback<MouseEventArgs>.Empty)
            .Add(x => x.ChildContent, Label));

        Assert.Equal("button", defaultCut.Find("button").GetAttribute("type"));
        Assert.Equal("submit", submitCut.Find("button").GetAttribute("type"));
    }
}
