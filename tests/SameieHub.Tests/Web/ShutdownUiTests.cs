using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using SameieHub.Infrastructure.Restore;
using SameieHub.Tests.Fakes;
using SameieHub.Web.Components.Layout;
using SameieHub.Web.Components.Pages;
using SameieHub.Web.Hosting;

namespace SameieHub.Tests.Web;

/// <summary>
/// The shutdown button and the about page in the side menu, the message that is shown before the program stops, and the difference
/// between the development start (nothing here can stop the server) and the installed Windows program.
/// </summary>
public class ShutdownUiTests : UiTestBase
{
    private const string NoticeModule = "./Components/Layout/ShutdownNotice.razor.js";

    private FakeDatabaseRestore FakeRestore => (FakeDatabaseRestore)Services.GetService(typeof(IDatabaseRestore))!;

    // The side menu

    [Fact]
    public void InDevelopment_TheMenuHasTheAboutLink_ButNoWayToStopTheServer()
    {
        var cut = Render<NavMenu>();

        var about = cut.Find("[data-testid=about-link]");
        Assert.Equal("/om", about.GetAttribute("href"));
        Assert.Contains("Om SameieHub", about.TextContent);
        Assert.Empty(cut.FindAll("[data-testid=shutdown-open]"));
        Assert.DoesNotContain("Avslutt", cut.Markup);
    }

    [Fact]
    public void InWindowsMode_TheShutdownIsAButton_NotALink_AndNothingHappensUntilItIsConfirmed()
    {
        UseShutdown(canShutDown: true);
        var cut = Render<NavMenu>();

        var button = cut.Find("[data-testid=shutdown-open]");

        Assert.Equal("button", button.TagName.ToLowerInvariant());
        Assert.Equal("button", button.GetAttribute("type"));
        Assert.Contains("Avslutt SameieHub", button.TextContent);
        Assert.DoesNotContain("Avslutt", string.Join(" ", cut.FindAll("#main-nav a").Select(link => link.TextContent)));
        Assert.DoesNotContain(cut.FindAll("a"), link => link.TextContent.Contains("Avslutt", StringComparison.Ordinal));
        Assert.False(Shutdown.IsRequested);
    }

    [Fact]
    public void Clicking_AsksFirst_AndExplainsThatNoDataIsLost()
    {
        UseShutdown(canShutDown: true);
        var cut = Render<NavMenu>();

        cut.Find("[data-testid=shutdown-open]").Click();

        var confirmation = cut.Find("[data-testid=shutdown-confirm]");
        Assert.Contains("Avslutte SameieHub?", confirmation.TextContent);
        Assert.Contains("Dataene dine endres ikke", confirmation.TextContent);
        Assert.Contains("Start-menyen", confirmation.TextContent);
        Assert.NotNull(cut.Find("#confirm-shutdown"));
        Assert.NotNull(cut.Find("#cancel-shutdown"));
        Assert.False(Shutdown.IsRequested);
        Assert.Equal(0, Lifetime.StopCalls);
    }

    [Fact]
    public void Cancelling_ClosesTheQuestion_AndLeavesTheProgramRunning()
    {
        UseShutdown(canShutDown: true);
        var cut = Render<NavMenu>();
        cut.Find("[data-testid=shutdown-open]").Click();

        cut.Find("#cancel-shutdown").Click();

        Assert.Empty(cut.FindAll("[data-testid=shutdown-confirm]"));
        Assert.NotNull(cut.Find("[data-testid=shutdown-open]"));
        Assert.False(Shutdown.IsRequested);
        Assert.Equal(0, Lifetime.StopCalls);
    }

    [Fact]
    public void TheEscapeKey_ClosesTheQuestion_WithoutStopping()
    {
        UseShutdown(canShutDown: true);
        var cut = Render<NavMenu>();
        cut.Find("[data-testid=shutdown-open]").Click();

        cut.Find("[data-testid=shutdown-confirm]").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.Empty(cut.FindAll("[data-testid=shutdown-confirm]"));
        Assert.False(Shutdown.IsRequested);
    }

    [Fact]
    public async Task Confirming_AsksTheProgramToStop_Once()
    {
        UseShutdown(canShutDown: true);
        var cut = Render<NavMenu>();
        cut.Find("[data-testid=shutdown-open]").Click();

        cut.Find("#confirm-shutdown").Click();

        Assert.True(Shutdown.IsRequested);
        Assert.False(Shutdown.RestartRequested);
        Assert.True(await Lifetime.WaitForStopAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(1, Lifetime.StopCalls);
    }

    [Fact]
    public void TheMobileMenu_ClosesWhenTheAboutPageIsChosen_AndShowsTheFooterOnlyWhenOpen()
    {
        var cut = Render<NavMenu>();
        Assert.DoesNotContain("open", cut.Find("[data-testid=sidebar-footer]").ClassName);

        cut.Find("button.menu-toggle").Click();
        Assert.Contains("open", cut.Find("[data-testid=sidebar-footer]").ClassName);

        cut.Find("[data-testid=about-link]").Click();
        Assert.DoesNotContain("open", cut.Find("[data-testid=sidebar-footer]").ClassName);
    }

    // The message before the program stops

    [Fact]
    public void WhenTheShutdownIsRequested_EveryOpenPageIsReplacedByAMessage_ThatSaysWhatToDoNext()
    {
        UseShutdown(canShutDown: true);
        var module = JSInterop.SetupModule(NoticeModule);
        module.SetupVoid("markStopped");
        var cut = Render<MainLayout>(parameters => parameters.Add(layout => layout.Body, (RenderFragment)(builder => builder.AddMarkupContent(0, "<p>Sidens innhold</p>"))));
        Assert.Contains("Sidens innhold", cut.Markup);
        Assert.Empty(cut.FindAll("[data-testid=shutdown-notice]"));

        Shutdown.Request(restart: false);

        cut.WaitForElement("[data-testid=shutdown-notice]");
        var notice = cut.Find("[data-testid=shutdown-notice]");
        Assert.Contains("SameieHub avsluttes", notice.TextContent);
        Assert.Contains("lukke denne fanen", notice.TextContent);
        Assert.Contains("Start-menyen", notice.TextContent);
        Assert.DoesNotContain("Sidens innhold", cut.Markup);
        Assert.Empty(cut.FindAll("[data-testid=shutdown-open]"));
        module.VerifyInvoke("markStopped");
    }

    [Fact]
    public void TheMessageForARestart_SaysThatTheProgramStartsAgainAndOpensANewTab()
    {
        UseShutdown(canShutDown: true, canRestart: true);
        JSInterop.SetupModule(NoticeModule).SetupVoid("markStopped");
        var cut = Render<MainLayout>(parameters => parameters.Add(layout => layout.Body, (RenderFragment)(builder => builder.AddMarkupContent(0, "<p>x</p>"))));

        Shutdown.Request(restart: true);

        cut.WaitForElement("[data-testid=shutdown-notice]");
        var notice = cut.Find("[data-testid=shutdown-notice]").TextContent;
        Assert.Contains("starter på nytt", notice);
        Assert.Contains("ny fane", notice);
        Assert.Contains("Start-menyen", notice);
        Assert.DoesNotContain("SameieHub avsluttes", notice);
        Assert.Contains("ikke endret av omstarten", notice);
        Assert.DoesNotContain("gjenoppretting", notice);
    }

    [Fact]
    public void TheMessageForARestartWithAWaitingRestore_SaysTheRestoreIsCarriedOutAtStartup_NotThatNothingChanges()
    {
        UseShutdown(canShutDown: true, canRestart: true);
        JSInterop.SetupModule(NoticeModule).SetupVoid("markStopped");
        FakeRestore.Pending = new PendingRestore("gyldig.db", new DateTime(2026, 11, 27, 9, 0, 0, DateTimeKind.Utc), false);
        var cut = Render<MainLayout>(parameters => parameters.Add(layout => layout.Body, (RenderFragment)(builder => builder.AddMarkupContent(0, "<p>x</p>"))));

        Shutdown.Request(restart: true);

        cut.WaitForElement("[data-testid=shutdown-notice]");
        var notice = cut.Find("[data-testid=shutdown-notice]").TextContent;
        Assert.Contains("gjennomføres under oppstarten", notice);
        Assert.DoesNotContain("ikke endret av omstarten", notice);
    }

    [Fact]
    public void AWaitingRestore_IsMentionedInTheShutdownMessage_BecauseItIsCarriedOutAtTheNextStart()
    {
        UseShutdown(canShutDown: true);
        JSInterop.SetupModule(NoticeModule).SetupVoid("markStopped");
        FakeRestore.Pending = new PendingRestore("gyldig.db", new DateTime(2026, 11, 27, 9, 0, 0, DateTimeKind.Utc), false);
        var cut = Render<MainLayout>(parameters => parameters.Add(layout => layout.Body, (RenderFragment)(builder => builder.AddMarkupContent(0, "<p>x</p>"))));

        Shutdown.Request(restart: false);

        cut.WaitForElement("[data-testid=shutdown-notice]");
        var notice = cut.Find("[data-testid=shutdown-notice]").TextContent;
        Assert.Contains("gjenoppretting", notice);
        Assert.Contains("første gang", notice);
        Assert.NotNull(FakeRestore.Pending);
    }

    [Fact]
    public void WithoutAWaitingRestore_TheMessageDoesNotMentionOne()
    {
        UseShutdown(canShutDown: true);
        JSInterop.SetupModule(NoticeModule).SetupVoid("markStopped");
        var cut = Render<MainLayout>(parameters => parameters.Add(layout => layout.Body, (RenderFragment)(builder => builder.AddMarkupContent(0, "<p>x</p>"))));

        Shutdown.Request(restart: false);

        cut.WaitForElement("[data-testid=shutdown-notice]");
        Assert.DoesNotContain("gjenoppretting", cut.Find("[data-testid=shutdown-notice]").TextContent);
    }

    [Fact]
    public void InDevelopment_ARequestChangesNothingOnThePage()
    {
        var cut = Render<MainLayout>(parameters => parameters.Add(layout => layout.Body, (RenderFragment)(builder => builder.AddMarkupContent(0, "<p>Sidens innhold</p>"))));

        Shutdown.Request(restart: false);

        Assert.Contains("Sidens innhold", cut.Markup);
        Assert.Empty(cut.FindAll("[data-testid=shutdown-notice]"));
        Assert.Equal(0, Lifetime.StopCalls);
    }

    // About

    [Fact]
    public void TheAboutPage_ShowsTheNameTheInstalledVersionAndThatTheDataStaysOnThisComputer()
    {
        var cut = Render<Om>();

        Assert.Contains("SameieHub", cut.Find("h1").TextContent);
        Assert.Equal(AppVersion.Current, cut.Find("[data-testid=about-version]").TextContent.Trim());
        Assert.Contains("lagres lokalt", cut.Markup);
        Assert.Contains("README", cut.Find("[data-testid=about-help]").TextContent);
    }

    [Fact]
    public void TheAboutPage_ShowsNoSecretsOrTechnicalDetails()
    {
        UseShutdown(canShutDown: true);

        var markup = Render<Om>().Markup;

        foreach (var forbidden in new[] { "cookie", "token", "engangskode", "launch", "127.0.0.1", "C:\\", "AppData", "pipe" })
        {
            Assert.DoesNotContain(forbidden, markup, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void WhenTheProgramRefusesTheShutdown_TheMenuSaysSoInsteadOfDoingNothing()
    {
        Services.AddSingleton(new ShutdownService(Lifetime, NullLogger<ShutdownService>.Instance, canShutDown: true, canRestart: false, TimeSpan.Zero, () => false));
        var cut = Render<NavMenu>();
        cut.Find("[data-testid=shutdown-open]").Click();

        cut.Find("#confirm-shutdown").Click();

        Assert.Contains("kunne ikke avsluttes", cut.Find("[data-testid=shutdown-refused]").TextContent);
        Assert.Equal(0, Lifetime.StopCalls);
    }
}
