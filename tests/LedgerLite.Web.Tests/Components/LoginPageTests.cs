using Bunit;
using LedgerLite.Web.Client.Pages;
using LedgerLite.Web.Client.Services.Auth;
using LedgerLite.Web.Tests.Infrastructure;

namespace LedgerLite.Web.Tests.Components;

public sealed class LoginPageTests
{
    private static IRenderedComponent<Login> RenderLoginPage(AppTestContext ctx)
    {
        // Anonymous: the login screen must not be preempted by an auth-state redirect.
        ctx.SetAuthenticationState(null);
        return ctx.RenderComponent<Login>();
    }

    private static IElement EmailInput(IRenderedComponent<Login> cut) =>
        cut.Find("input[placeholder='you@example.com']");

    private static IElement PasswordInput(IRenderedComponent<Login> cut) =>
        cut.Find("input[placeholder='••••••••']");

    [Fact]
    public void Renders_headline_and_demo_chip()
    {
        using var ctx = new AppTestContext();
        var cut = RenderLoginPage(ctx);

        Assert.Contains("Welcome back", cut.Markup);
        Assert.Contains("Sign in to your ledger.", cut.Markup);
        Assert.Contains("Use the demo account", cut.Markup);
        // Markup keeps "@" as the &#64; entity, so assert on decoded text content.
        var demoChip = cut.FindAll("button").Single(b => b.TextContent.Contains("Use the demo account"));
        Assert.Contains("demo@ledgerlite.io / Demo123!", demoChip.TextContent);
    }

    [Fact]
    public async Task Submitting_empty_fields_shows_error_and_skips_auth_service()
    {
        using var ctx = new AppTestContext();
        var cut = RenderLoginPage(ctx);

        await cut.Find("form").SubmitAsync();

        Assert.Contains("Please fill in all fields.", cut.Markup);
        await ctx.Auth.DidNotReceiveWithAnyArgs().LoginAsync(default!, default!, default);
    }

    [Fact]
    public async Task Demo_chip_prefills_credentials()
    {
        using var ctx = new AppTestContext();
        var cut = RenderLoginPage(ctx);

        await cut.FindAll("button").Single(b => b.TextContent.Contains("Use the demo account")).ClickAsync(new MouseEventArgs());

        Assert.Equal("demo@ledgerlite.io", EmailInput(cut).GetAttribute("value"));
        Assert.Equal("Demo123!", PasswordInput(cut).GetAttribute("value"));
    }

    [Fact]
    public async Task Demo_credentials_submit_calls_LoginAsync_with_matching_values()
    {
        using var ctx = new AppTestContext();
        ctx.Auth.LoginAsync("demo@ledgerlite.io", "Demo123!", Arg.Any<CancellationToken>())
            .Returns(AuthResult.Ok);
        var cut = RenderLoginPage(ctx);

        await cut.FindAll("button").Single(b => b.TextContent.Contains("Use the demo account")).ClickAsync(new MouseEventArgs());
        await cut.Find("form").SubmitAsync();

        await ctx.Auth.Received(1).LoginAsync("demo@ledgerlite.io", "Demo123!", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Failed_login_shows_error_text()
    {
        using var ctx = new AppTestContext();
        ctx.Auth
            .LoginAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(AuthResult.Fail("Invalid email or password."));
        var cut = RenderLoginPage(ctx);

        // Trailing/leading whitespace must be trimmed before hitting the service.
        await EmailInput(cut).InputAsync(new ChangeEventArgs { Value = "  demo@ledgerlite.io  " });
        await PasswordInput(cut).InputAsync(new ChangeEventArgs { Value = "Demo123!" });
        await cut.Find("form").SubmitAsync();

        Assert.Contains("Invalid email or password.", cut.Find("div[role='alert']").TextContent);
        await ctx.Auth.Received(1).LoginAsync("demo@ledgerlite.io", "Demo123!", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Successful_login_does_not_throw_and_shows_no_error()
    {
        using var ctx = new AppTestContext();
        ctx.Auth
            .LoginAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(AuthResult.Ok);
        var cut = RenderLoginPage(ctx);

        await EmailInput(cut).InputAsync(new ChangeEventArgs { Value = "user@ledgerlite.io" });
        await PasswordInput(cut).InputAsync(new ChangeEventArgs { Value = "correct horse" });
        await cut.Find("form").SubmitAsync();

        Assert.Empty(cut.FindAll("div[role='alert']"));
        Assert.DoesNotContain("Please fill in all fields.", cut.Markup);
    }

    [Fact]
    public async Task Toggle_to_register_adds_display_name_and_calls_RegisterAsync()
    {
        using var ctx = new AppTestContext();
        ctx.Auth
            .RegisterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(AuthResult.Ok);
        var cut = RenderLoginPage(ctx);

        await cut.FindAll("button").Single(b => b.TextContent.Trim() == "Create one").ClickAsync(new MouseEventArgs());

        cut.WaitForAssertion(() => Assert.Contains("Create your account", cut.Markup));
        Assert.NotNull(cut.Find("input[placeholder='Alex Morgan']"));

        await cut.Find("input[placeholder='Alex Morgan']").InputAsync(new ChangeEventArgs { Value = "  Alex Morgan  " });
        await EmailInput(cut).InputAsync(new ChangeEventArgs { Value = "alex@ledgerlite.io" });
        await PasswordInput(cut).InputAsync(new ChangeEventArgs { Value = "Secret1!" });
        await cut.Find("form").SubmitAsync();

        await ctx.Auth.Received(1).RegisterAsync(
            "alex@ledgerlite.io", "Alex Morgan", "Secret1!", Arg.Any<CancellationToken>());

        // After a successful registration the form flips back to sign-in with a toast.
        cut.WaitForAssertion(() => Assert.Contains("Welcome back", cut.Markup));
        Assert.Contains(
            "Account created. Sign in to continue.",
            Assert.Single(ctx.Toast.GetToasts()).Message);
    }
}
