using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SameieHub.Domain;
using SameieHub.Tests.Fakes;
using SameieHub.Web.Components.Pages.TaskItems;

namespace SameieHub.Tests.Web;

public class TaskCreateTests : UiTestBase
{
    private IRenderedComponent<TaskCreate> RenderLoaded(string query = "")
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo("/oppgaver/ny" + query);
        var cut = Render<TaskCreate>();
        cut.WaitForElement("#title");

        return cut;
    }

    // Registration

    [Fact]
    public async Task Submit_OnlyTitle_StoresTaskWithDefaultsAndGoesToDetailPage()
    {
        var cut = RenderLoaded();

        cut.Find("#title").Change("  Rense takrenner  ");
        cut.Find("form").Submit();

        var task = Assert.Single(await Tasks.GetAllAsync());
        cut.WaitForAssertion(() => Assert.Equal($"/oppgaver/{task.Id}?status=opprettet", CurrentPath));
        Assert.Equal("Rense takrenner", task.Title);
        Assert.Equal(TaskItemStatus.NotStarted, task.Status);
        Assert.Equal(TaskItemCategory.General, task.Category);
        Assert.Equal(TaskItemPriority.Normal, task.Priority);
        Assert.Null(task.DueDate);
        Assert.Null(task.HousingUnitId);
        Assert.Null(task.BoardCaseId);
    }

    [Fact]
    public async Task Submit_AllFields_StoresEverything()
    {
        var unit = await Service.RegisterAsync("Storgata 1A");
        var boardCase = await BoardCases.RegisterAsync(new BoardCaseDetails { Title = "Maling av porter" });
        var cut = RenderLoaded();

        cut.Find("#title").Change("Rense takrenner");
        cut.Find("#description").Change("Fiktiv beskrivelse.");
        cut.Find("#category").Change("Maintenance");
        cut.Find("#priority").Change("High");
        cut.Find("#dueDate").Change("2026-12-01");
        cut.Find("#assignedTo").Change("Test Person");
        cut.Find("#housingUnitId").Change(unit.Id.ToString());
        cut.Find("#boardCaseId").Change(boardCase.Id.ToString());
        cut.Find("#notes").Change("Fiktive merknader.");
        cut.Find("form").Submit();

        var task = Assert.Single(await Tasks.GetAllAsync());
        Assert.Equal("Fiktiv beskrivelse.", task.Description);
        Assert.Equal(TaskItemCategory.Maintenance, task.Category);
        Assert.Equal(TaskItemPriority.High, task.Priority);
        Assert.Equal(new DateOnly(2026, 12, 1), task.DueDate);
        Assert.Equal("Test Person", task.AssignedTo);
        Assert.Equal(unit.Id, task.HousingUnitId);
        Assert.Equal(boardCase.Id, task.BoardCaseId);
        Assert.Equal("Fiktive merknader.", task.Notes);
    }

    [Fact]
    public async Task Render_Fields_UseSuitableInputTypes()
    {
        await Service.RegisterAsync("Storgata 1A");

        var cut = RenderLoaded();

        Assert.Equal("date", cut.Find("#dueDate").GetAttribute("type"));
        Assert.Equal("TEXTAREA", cut.Find("#description").TagName);
        Assert.Equal("TEXTAREA", cut.Find("#notes").TagName);
        Assert.Equal("SELECT", cut.Find("#category").TagName);
        Assert.Equal("SELECT", cut.Find("#priority").TagName);
        Assert.Equal("true", cut.Find("#title").GetAttribute("aria-required"));
    }

    [Fact]
    public void Render_CategoryAndPriorityChoices_UseNorwegianNames()
    {
        var cut = RenderLoaded();

        Assert.Equal(["Generell", "Vedlikehold"], cut.FindAll("#category option").Select(option => option.TextContent));
        Assert.Equal(["Lav", "Normal", "Høy"], cut.FindAll("#priority option").Select(option => option.TextContent));
    }

    [Fact]
    public async Task Render_HousingUnitChoices_OnlyContainActiveUnitsAndNone()
    {
        await Service.RegisterAsync("Storgata 1A");
        var inactive = await Service.RegisterAsync("Storgata 1B");
        await Service.DeactivateAsync(inactive.Id);

        var cut = RenderLoaded();

        Assert.Equal(["Ingen boenhet", "Storgata 1A"], cut.FindAll("#housingUnitId option").Select(option => option.TextContent));
    }

    [Fact]
    public async Task Render_BoardCaseChoices_ContainAllCasesAlsoClosedOnes()
    {
        var open = await BoardCases.RegisterAsync(new BoardCaseDetails { Title = "Åpen sak" });
        var closed = await BoardCases.RegisterAsync(new BoardCaseDetails { Title = "Avsluttet sak" });
        await BoardCases.ChangeStatusAsync(closed.Id, BoardCaseStatus.Closed);

        var cut = RenderLoaded();

        Assert.Equal(
            ["Ingen styresak", $"#{open.Id} Åpen sak", $"#{closed.Id} Avsluttet sak"],
            cut.FindAll("#boardCaseId option").Select(option => option.TextContent));
    }

    [Fact]
    public async Task Submit_BoardCaseForAnotherUnit_IsAllowedAndNothingIsCopiedFromTheCase()
    {
        var caseUnit = await Service.RegisterAsync("Storgata 1B");
        var boardCase = await BoardCases.RegisterAsync(new BoardCaseDetails { Title = "Maling", HousingUnitId = caseUnit.Id });
        var cut = RenderLoaded();

        cut.Find("#title").Change("Oppgave");
        cut.Find("#boardCaseId").Change(boardCase.Id.ToString());
        cut.Find("form").Submit();

        var task = Assert.Single(await Tasks.GetAllAsync());
        Assert.Equal(boardCase.Id, task.BoardCaseId);
        Assert.Null(task.HousingUnitId);
    }

    // Validation and errors

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Submit_EmptyTitle_ShowsMessageKeepsInputAndStoresNothing(string title)
    {
        var cut = RenderLoaded();

        cut.Find("#title").Change(title);
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Equal("Tittelen må fylles ut.", cut.Find(".validation-message").TextContent));
        Assert.Equal(title, cut.Find("#title").GetAttribute("value"));
        Assert.Empty(await Tasks.GetAllAsync());
    }

    [Fact]
    public async Task Submit_DueDateOutOfRange_ShowsNorwegianMessageAtTheFieldAndStoresNothing()
    {
        var cut = RenderLoaded();
        cut.Find("#title").Change("Oppgave");

        cut.Find("#dueDate").Change("1999-01-01");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() =>
            Assert.Equal("Fristen må være mellom 01.01.2000 og 31.12.2100.", cut.Find(".validation-message").TextContent));
        Assert.Equal("Oppgave", cut.Find("#title").GetAttribute("value"));
        Assert.Empty(await Tasks.GetAllAsync());
    }

    [Fact]
    public async Task Submit_AfterCorrectingTheDate_Saves()
    {
        var cut = RenderLoaded();
        cut.Find("#title").Change("Oppgave");
        cut.Find("#dueDate").Change("1999-01-01");
        cut.Find("form").Submit();
        cut.WaitForElement(".validation-message");

        cut.Find("#dueDate").Change("2026-12-03");
        cut.Find("form").Submit();

        Assert.Single(await Tasks.GetAllAsync());
    }

    [Fact]
    public async Task Submit_TitleTooLong_ShowsMessageAtTheTitle()
    {
        var cut = RenderLoaded();

        cut.Find("#title").Change(new string('A', TaskItem.MaxTitleLength + 1));
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Contains("maks 200 tegn", cut.Find(".validation-message").TextContent));
        Assert.Empty(await Tasks.GetAllAsync());
    }

    [Fact]
    public async Task Submit_HousingUnitDeactivatedWhileFormWasOpen_ShowsMessageAtTheFieldAndStoresNothing()
    {
        var unit = await Service.RegisterAsync("Storgata 1A");
        var cut = RenderLoaded();
        cut.Find("#title").Change("Oppgave");
        cut.Find("#housingUnitId").Change(unit.Id.ToString());
        await Service.DeactivateAsync(unit.Id);

        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Contains("Boenheten finnes ikke eller er inaktiv", cut.Find(".validation-message").TextContent));
        Assert.Empty(await Tasks.GetAllAsync());
    }

    [Fact]
    public async Task Submit_BoardCaseDeletedWhileFormWasOpen_ShowsMessageAtTheFieldAndStoresNothing()
    {
        var boardCase = await BoardCases.RegisterAsync(new BoardCaseDetails { Title = "Maling" });
        var cut = RenderLoaded();
        cut.Find("#title").Change("Oppgave");
        cut.Find("#boardCaseId").Change(boardCase.Id.ToString());
        await BoardCases.DeleteAsync(boardCase.Id);

        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Contains("Styresaken finnes ikke", cut.Find(".validation-message").TextContent));
        Assert.Empty(await Tasks.GetAllAsync());
    }

    [Fact]
    public void Submit_DatabaseUnavailable_ShowsErrorAndKeepsInput()
    {
        var cut = RenderLoaded();
        TaskRepository.IsFailing = true;

        cut.Find("#title").Change("Oppgave");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Contains("Kunne ikke lagre", cut.Find("[role=alert]").TextContent));
        Assert.Equal("Oppgave", cut.Find("#title").GetAttribute("value"));
        Assert.DoesNotContain("status=opprettet", CurrentPath);
    }

    [Fact]
    public void Submit_DatabaseFailure_DoesNotLogTheEnteredText()
    {
        var logger = new CapturingLoggerProvider();
        Services.AddLogging(builder => builder.AddProvider(logger));
        var cut = RenderLoaded();
        TaskRepository.IsFailing = true;

        cut.Find("#title").Change("Hemmelig tittel");
        cut.Find("#notes").Change("Hemmelige merknader");
        cut.Find("form").Submit();
        cut.WaitForElement("[role=alert]");

        Assert.NotEmpty(logger.Messages);
        Assert.DoesNotContain(logger.Messages, message => message.Contains("Hemmelig"));
    }

    [Fact]
    public async Task Submit_WhileTheFirstSaveIsStillRunning_StoresOnlyOneTaskAndDisablesTheButton()
    {
        var blocker = new TaskCompletionSource();
        TaskRepository.AddBlocker = blocker;
        var cut = RenderLoaded();
        cut.Find("#title").Change("Oppgave");

        cut.Find("form").Submit();
        cut.Find("form").Submit();

        Assert.True(cut.Find("button[type=submit]").HasAttribute("disabled"));
        Assert.Contains("Lagrer", cut.Find("button[type=submit]").TextContent);
        blocker.SetResult();
        cut.WaitForAssertion(() => Assert.StartsWith("/oppgaver/1", CurrentPath));
        Assert.Single(await Tasks.GetAllAsync());
    }

    [Fact]
    public void Render_DataUnavailable_ShowsErrorAndRetryShowsTheForm()
    {
        Repository.IsFailing = true;
        var cut = Render<TaskCreate>();
        cut.WaitForAssertion(() => Assert.Contains("Kunne ikke hente skjemaet", cut.Find("[role=alert]").TextContent));

        Repository.IsFailing = false;
        cut.Find("button").Click();

        cut.WaitForElement("#title");
    }

    [Fact]
    public void Render_HasLinkBackToList()
    {
        var cut = RenderLoaded();

        Assert.Equal("/oppgaver", cut.Find("a.back-link").GetAttribute("href"));
        Assert.Equal("/oppgaver", cut.Find(".form-actions a").GetAttribute("href"));
    }

    // Preselection from the board case page

    [Fact]
    public async Task Shortcut_ExistingBoardCase_IsPreselectedAndSavedWithTheTask()
    {
        var boardCase = await BoardCases.RegisterAsync(new BoardCaseDetails { Title = "Maling" });

        var cut = RenderLoaded($"?styresak={boardCase.Id}");

        Assert.Equal(boardCase.Id.ToString(), cut.Find("#boardCaseId").GetAttribute("value"));
        Assert.Empty(cut.FindAll(".alert-warning"));
        Assert.Equal($"/styresaker/{boardCase.Id}", cut.Find(".form-actions a").GetAttribute("href"));
        cut.Find("#title").Change("Oppgave");
        cut.Find("form").Submit();
        Assert.Equal(boardCase.Id, Assert.Single(await Tasks.GetAllAsync()).BoardCaseId);
    }

    [Fact]
    public async Task Shortcut_ClosedBoardCase_IsPreselected()
    {
        var boardCase = await BoardCases.RegisterAsync(new BoardCaseDetails { Title = "Maling" });
        await BoardCases.ChangeStatusAsync(boardCase.Id, BoardCaseStatus.Closed);

        var cut = RenderLoaded($"?styresak={boardCase.Id}");

        Assert.Equal(boardCase.Id.ToString(), cut.Find("#boardCaseId").GetAttribute("value"));
    }

    [Theory]
    [InlineData("999")]
    [InlineData("abc")]
    [InlineData("-1")]
    [InlineData("0")]
    public void Shortcut_InvalidBoardCase_IsNotPreselectedAndShowsNotice(string value)
    {
        var cut = RenderLoaded($"?styresak={value}");

        Assert.Equal("", cut.Find("#boardCaseId").GetAttribute("value") ?? "");
        Assert.Contains("Styresaken fra lenken finnes ikke", cut.Find(".alert-warning").TextContent);
    }

    // Preselection from the housing unit page

    [Fact]
    public async Task Shortcut_ActiveHousingUnit_IsPreselectedAndSavedWithTheTask()
    {
        var unit = await Service.RegisterAsync("Storgata 1A");
        await Service.RegisterAsync("Storgata 1B");

        var cut = RenderLoaded($"?boenhet={unit.Id}");

        Assert.Equal(unit.Id.ToString(), cut.Find("#housingUnitId").GetAttribute("value"));
        Assert.Empty(cut.FindAll(".alert-warning"));
        Assert.Equal($"/boenheter/{unit.Id}", cut.Find(".form-actions a").GetAttribute("href"));
        cut.Find("#title").Change("Oppgave");
        cut.Find("form").Submit();
        Assert.Equal(unit.Id, Assert.Single(await Tasks.GetAllAsync()).HousingUnitId);
    }

    [Fact]
    public async Task Shortcut_InactiveHousingUnit_IsNotPreselectedNotOfferedAndShowsNotice()
    {
        var inactive = await Service.RegisterAsync("Storgata 1A");
        await Service.DeactivateAsync(inactive.Id);

        var cut = RenderLoaded($"?boenhet={inactive.Id}");

        Assert.Equal("", cut.Find("#housingUnitId").GetAttribute("value") ?? "");
        Assert.DoesNotContain(cut.FindAll("#housingUnitId option"), option => option.TextContent.Contains("Storgata 1A"));
        Assert.Contains("Boenheten fra lenken finnes ikke eller er inaktiv", cut.Find(".alert-warning").TextContent);
        cut.Find("#title").Change("Oppgave");
        cut.Find("form").Submit();
        Assert.Null(Assert.Single(await Tasks.GetAllAsync()).HousingUnitId);
    }

    [Theory]
    [InlineData("999")]
    [InlineData("abc")]
    [InlineData("-5")]
    public void Shortcut_InvalidHousingUnit_IsNotPreselectedAndShowsNotice(string value)
    {
        var cut = RenderLoaded($"?boenhet={value}");

        Assert.Equal("", cut.Find("#housingUnitId").GetAttribute("value") ?? "");
        Assert.Contains("Boenheten fra lenken finnes ikke eller er inaktiv", cut.Find(".alert-warning").TextContent);
    }

    [Fact]
    public async Task Shortcut_BothParameters_PreselectsBoth()
    {
        var unit = await Service.RegisterAsync("Storgata 1A");
        var boardCase = await BoardCases.RegisterAsync(new BoardCaseDetails { Title = "Maling" });

        var cut = RenderLoaded($"?boenhet={unit.Id}&styresak={boardCase.Id}");

        Assert.Equal(unit.Id.ToString(), cut.Find("#housingUnitId").GetAttribute("value"));
        Assert.Equal(boardCase.Id.ToString(), cut.Find("#boardCaseId").GetAttribute("value"));
    }

    [Fact]
    public void Shortcut_NoParameters_ShowsNoNotice()
    {
        var cut = RenderLoaded();

        Assert.Empty(cut.FindAll(".alert-warning"));
    }
}
