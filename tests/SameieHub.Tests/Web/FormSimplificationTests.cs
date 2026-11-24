using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using SameieHub.Domain;
using SameieHub.Web.Components.Pages.BoardCases;
using SameieHub.Web.Components.Pages.HousingUnits;
using SameieHub.Web.Components.Pages.TaskItems;

namespace SameieHub.Tests.Web;

/// <summary>
/// The simplified registration forms (R4): what is shown first, what is under «Flere valg», what is suggested from the context, and that
/// a mistake in a field under «Flere valg» opens it without losing what was typed. The rules themselves are tested in the older form tests.
/// </summary>
public class FormSimplificationTests : UiTestBase
{
    private IRenderedComponent<TaskCreate> RenderTaskCreate(string query = "")
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo("/oppgaver/ny" + query);
        var cut = Render<TaskCreate>();
        cut.WaitForElement("#title");

        return cut;
    }

    private IRenderedComponent<TaskEdit> RenderTaskEdit(int id)
    {
        var cut = Render<TaskEdit>(parameters => parameters.Add(page => page.Id, id));
        cut.WaitForElement("#title");

        return cut;
    }

    private IRenderedComponent<BoardCaseCreate> RenderCaseCreate(string query = "")
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo("/styresaker/ny" + query);
        var cut = Render<BoardCaseCreate>();
        cut.WaitForElement("#title");

        return cut;
    }

    private IRenderedComponent<BoardCaseEdit> RenderCaseEdit(int id)
    {
        var cut = Render<BoardCaseEdit>(parameters => parameters.Add(page => page.Id, id));
        cut.WaitForElement("#title");

        return cut;
    }

    private IRenderedComponent<HousingUnitEdit> RenderUnitEdit(int id)
    {
        var cut = Render<HousingUnitEdit>(parameters => parameters.Add(page => page.Id, id));
        cut.WaitForElement("#address");

        return cut;
    }

    private static bool IsInsideMoreOptions<T>(IRenderedComponent<T> cut, string selector) where T : IComponent
    {
        return cut.FindAll($"details.disclosure {selector}").Count > 0;
    }

    private static bool IsMoreOptionsOpen<T>(IRenderedComponent<T> cut) where T : IComponent
    {
        return cut.Find("details.disclosure").HasAttribute("open");
    }

    // Task form

    [Fact]
    public void TaskForm_Direct_ShowsTheEssentialFieldsFirstAndTheRestUnderMoreOptions()
    {
        var cut = RenderTaskCreate();

        foreach (var visible in new[] { "#title", "#assignedTo", "#dueDate", "#scope-whole", "#scope-unit" })
        {
            Assert.False(IsInsideMoreOptions(cut, visible), visible);
        }

        foreach (var hidden in new[] { "#description", "#category", "#priority", "#notes", "#boardCaseId" })
        {
            Assert.True(IsInsideMoreOptions(cut, hidden), hidden);
        }

        Assert.False(IsMoreOptionsOpen(cut));
        Assert.Contains("Styresak", cut.Find("details.disclosure .disclosure-hint").TextContent);
    }

    [Fact]
    public void TaskForm_Direct_ExplainsWhatATaskIsAndLinksToBoardCases_AndNeedsNoBoardCase()
    {
        var cut = RenderTaskCreate();

        var text = cut.FindByTestId("task-explanation").TextContent;
        Assert.Contains("En oppgave er noe som skal gjøres.", text);
        Assert.Contains("Hvis styret først må behandle eller vedta noe, kan du registrere en styresak.", text);
        Assert.Equal("/styresaker/ny", cut.FindByTestId("task-explanation").QuerySelector("a")!.GetAttribute("href"));
        Assert.False(cut.HasTestId("case-context"));
        Assert.True(cut.IsWholeCooperativeChosen());
    }

    [Fact]
    public async Task TaskForm_OrdinaryTodo_WithAssigneeAndDueDateButNoBoardCase_IsStoredForTheWholeCooperative()
    {
        var cut = RenderTaskCreate();

        cut.Find("#title").Change("Kontrollere fellesbelysningen");
        cut.Find("#assignedTo").Change("Lise Prøve");
        cut.Find("#dueDate").Change("2026-12-05");
        cut.Find("form").Submit();

        var task = Assert.Single(await Tasks.GetAllAsync());
        Assert.Equal("Kontrollere fellesbelysningen", task.Title);
        Assert.Equal("Lise Prøve", task.AssignedTo);
        Assert.Equal(new DateOnly(2026, 12, 5), task.DueDate);
        Assert.Null(task.HousingUnitId);
        Assert.Null(task.BoardCaseId);
    }

    [Fact]
    public async Task TaskForm_FromABoardCase_ShowsTheCaseAndSuggestsItsUnit_WithTheCaseOutsideMoreOptions()
    {
        var unit = await Service.RegisterAsync("Storgata 1A");
        var boardCase = await BoardCases.RegisterAsync(new BoardCaseDetails { Title = "Lekkasje", HousingUnitId = unit.Id });

        var cut = RenderTaskCreate($"?styresak={boardCase.Id}");

        var context = cut.FindByTestId("case-context").TextContent;
        Assert.Contains($"#{boardCase.Id} «Lekkasje»", context);
        Assert.Contains("Saken gjelder Storgata 1A", context);
        Assert.Contains("kan endre boenheten", context);
        Assert.False(cut.HasTestId("task-explanation"));
        Assert.False(IsInsideMoreOptions(cut, "#boardCaseId"));
        Assert.Equal(boardCase.Id.ToString(), cut.Find("#boardCaseId").GetAttribute("value"));
        Assert.Equal(unit.Id.ToString(), cut.SelectedHousingUnit());
        Assert.False(cut.IsWholeCooperativeChosen());
        Assert.Equal($"/styresaker/{boardCase.Id}", cut.Find("a.back-link").GetAttribute("href"));
    }

    [Fact]
    public async Task TaskForm_FromABoardCase_TheSuggestedUnitIsStoredWhenKept()
    {
        var unit = await Service.RegisterAsync("Storgata 1A");
        var boardCase = await BoardCases.RegisterAsync(new BoardCaseDetails { Title = "Lekkasje", HousingUnitId = unit.Id });
        var cut = RenderTaskCreate($"?styresak={boardCase.Id}");

        cut.Find("#title").Change("Kontakte rørlegger");
        cut.Find("form").Submit();

        var task = Assert.Single(await Tasks.GetAllAsync());
        Assert.Equal(boardCase.Id, task.BoardCaseId);
        Assert.Equal(unit.Id, task.HousingUnitId);
    }

    [Fact]
    public async Task TaskForm_FromABoardCase_TheUnitCanBeChangedToAnotherUnit()
    {
        var caseUnit = await Service.RegisterAsync("Storgata 1A");
        var otherUnit = await Service.RegisterAsync("Storgata 1B");
        var boardCase = await BoardCases.RegisterAsync(new BoardCaseDetails { Title = "Lekkasje", HousingUnitId = caseUnit.Id });
        var cut = RenderTaskCreate($"?styresak={boardCase.Id}");

        cut.Find("#title").Change("Sjekk naboen");
        cut.ChooseHousingUnit(otherUnit.Id);
        cut.Find("form").Submit();

        var task = Assert.Single(await Tasks.GetAllAsync());
        Assert.Equal(boardCase.Id, task.BoardCaseId);
        Assert.Equal(otherUnit.Id, task.HousingUnitId);
    }

    [Fact]
    public async Task TaskForm_FromABoardCase_TheUnitAndTheCaseLinkCanBothBeRemoved()
    {
        var unit = await Service.RegisterAsync("Storgata 1A");
        var boardCase = await BoardCases.RegisterAsync(new BoardCaseDetails { Title = "Lekkasje", HousingUnitId = unit.Id });
        var cut = RenderTaskCreate($"?styresak={boardCase.Id}");

        cut.Find("#title").Change("Uavhengig oppgave");
        cut.ChooseWholeCooperative();
        cut.Find("#boardCaseId").Change("");
        cut.Find("form").Submit();

        var task = Assert.Single(await Tasks.GetAllAsync());
        Assert.Null(task.HousingUnitId);
        Assert.Null(task.BoardCaseId);
    }

    [Fact]
    public async Task TaskForm_FromABoardCaseForTheWholeCooperative_SaysSoAndSuggestsNoUnit()
    {
        var boardCase = await BoardCases.RegisterAsync(new BoardCaseDetails { Title = "Vedtekter" });

        var cut = RenderTaskCreate($"?styresak={boardCase.Id}");

        Assert.Contains("Saken gjelder hele sameiet", cut.FindByTestId("case-context").TextContent);
        Assert.True(cut.IsWholeCooperativeChosen());
    }

    [Fact]
    public async Task TaskForm_FromABoardCaseWithAnInactiveUnit_DoesNotSuggestTheUnitAndSaysWhy()
    {
        var unit = await Service.RegisterAsync("Storgata 1A");
        var boardCase = await BoardCases.RegisterAsync(new BoardCaseDetails { Title = "Lekkasje", HousingUnitId = unit.Id });
        await Service.DeactivateAsync(unit.Id);

        var cut = RenderTaskCreate($"?styresak={boardCase.Id}");

        Assert.Contains("inaktiv, så boenheten er ikke foreslått", cut.FindByTestId("case-context").TextContent);
        Assert.True(cut.IsWholeCooperativeChosen());
    }

    [Fact]
    public async Task TaskForm_AUnitInTheAddressHasPriorityOverTheUnitOfTheCase()
    {
        var caseUnit = await Service.RegisterAsync("Storgata 1A");
        var addressUnit = await Service.RegisterAsync("Storgata 1B");
        var boardCase = await BoardCases.RegisterAsync(new BoardCaseDetails { Title = "Lekkasje", HousingUnitId = caseUnit.Id });

        var cut = RenderTaskCreate($"?styresak={boardCase.Id}&boenhet={addressUnit.Id}");

        Assert.Equal(addressUnit.Id.ToString(), cut.SelectedHousingUnit());
    }

    [Fact]
    public async Task TaskForm_ChoosingWholeCooperativeAndThenAUnitAgain_RemembersTheUnit()
    {
        var unit = await Service.RegisterAsync("Storgata 1A");
        var cut = RenderTaskCreate($"?boenhet={unit.Id}");
        Assert.Equal(unit.Id.ToString(), cut.SelectedHousingUnit());

        cut.ChooseWholeCooperative();
        Assert.Empty(cut.FindAll("#housingUnitId"));
        cut.Find("#scope-unit").Change(true);

        Assert.Equal(unit.Id.ToString(), cut.SelectedHousingUnit());
    }

    [Fact]
    public async Task TaskForm_AUnitIsChosenButNoneSelected_AsksForOneAndKeepsTheOtherValues()
    {
        await Service.RegisterAsync("Storgata 1A");
        var cut = RenderTaskCreate();
        cut.Find("#title").Change("Oppgave");
        cut.Find("#assignedTo").Change("Lise Prøve");
        cut.Find("#scope-unit").Change(true);

        cut.Find("form").Submit();

        Assert.Contains("Velg en boenhet, eller velg «Hele sameiet»", cut.FindByTestId("scope-error").TextContent);
        Assert.Empty(await Tasks.GetAllAsync());
        Assert.Equal("Oppgave", cut.Find("#title").GetAttribute("value"));
        Assert.Equal("Lise Prøve", cut.Find("#assignedTo").GetAttribute("value"));
    }

    [Fact]
    public void TaskForm_ErrorInAFieldUnderMoreOptions_OpensItShowsTheErrorThereAndKeepsEverythingTyped()
    {
        var cut = RenderTaskCreate();
        cut.Find("#title").Change("Oppgave");
        cut.Find("#assignedTo").Change("Lise Prøve");
        cut.Find("#priority").Change("High");
        cut.Find("#description").Change(new string('A', TaskItem.MaxDescriptionLength + 1));
        Assert.False(IsMoreOptionsOpen(cut));

        cut.Find("form").Submit();

        Assert.True(IsMoreOptionsOpen(cut));
        var message = cut.Find("details.disclosure .validation-message");
        Assert.Contains("Beskrivelsen kan ha maks", message.TextContent);
        Assert.Equal("Oppgave", cut.Find("#title").GetAttribute("value"));
        Assert.Equal("Lise Prøve", cut.Find("#assignedTo").GetAttribute("value"));
        Assert.Equal(TaskItemPriority.High.ToString(), cut.Find("#priority").GetAttribute("value"));
    }

    [Fact]
    public void TaskForm_MoreOptionsStayOpenAfterTheErrorIsCorrectedAndTheFormIsRenderedAgain()
    {
        var cut = RenderTaskCreate();
        cut.Find("#title").Change("Oppgave");
        cut.Find("#description").Change(new string('A', TaskItem.MaxDescriptionLength + 1));
        cut.Find("form").Submit();
        Assert.True(IsMoreOptionsOpen(cut));

        cut.Find("#description").Change("Kort beskrivelse");
        cut.Find("#title").Change("Oppgave 2");

        Assert.True(IsMoreOptionsOpen(cut));
        Assert.Equal("Kort beskrivelse", cut.Find("#description").GetAttribute("value"));
    }

    [Fact]
    public async Task TaskForm_ErrorInAVisibleField_DoesNotOpenMoreOptions()
    {
        var cut = RenderTaskCreate();
        cut.Find("#title").Change("Oppgave");
        cut.Find("#dueDate").Change("1999-01-01");

        cut.Find("form").Submit();

        Assert.Contains("Fristen må være mellom", cut.Find(".validation-message").TextContent);
        Assert.False(IsMoreOptionsOpen(cut));
        Assert.Empty(await Tasks.GetAllAsync());
    }

    [Fact]
    public async Task TaskEdit_ExistingValuesUnderMoreOptions_AreNotHidden_AndSavingChangesNothingElse()
    {
        var unit = await Service.RegisterAsync("Storgata 1A");
        var boardCase = await BoardCases.RegisterAsync(new BoardCaseDetails { Title = "Maling" });
        var task = await Tasks.RegisterAsync(new TaskItemDetails
        {
            Title = "Rense takrenner",
            Description = "Fiktiv beskrivelse.",
            Category = TaskItemCategory.Maintenance,
            Priority = TaskItemPriority.High,
            DueDate = new DateOnly(2026, 12, 1),
            AssignedTo = "Test Person",
            HousingUnitId = unit.Id,
            BoardCaseId = boardCase.Id,
            Notes = "Fiktive merknader."
        });
        var before = (await Tasks.GetByIdAsync(task.Id))!.ToDetails();

        var cut = RenderTaskEdit(task.Id);

        Assert.True(IsMoreOptionsOpen(cut));
        Assert.False(IsInsideMoreOptions(cut, "#boardCaseId"));
        Assert.Equal("Fiktiv beskrivelse.", cut.Find("#description").GetAttribute("value"));
        cut.Find("form").Submit();

        Assert.Equal(before, (await Tasks.GetByIdAsync(task.Id))!.ToDetails());
    }

    [Fact]
    public async Task TaskEdit_WithoutOptionalValues_KeepsMoreOptionsClosed_AndTheBoardCaseFieldUnderThem()
    {
        var task = await Tasks.RegisterAsync(new TaskItemDetails { Title = "Enkel oppgave" });

        var cut = RenderTaskEdit(task.Id);

        Assert.False(IsMoreOptionsOpen(cut));
        Assert.True(IsInsideMoreOptions(cut, "#boardCaseId"));
    }

    // Board case form

    [Fact]
    public void CaseForm_ShowsTitleDescriptionMeetingDateAndScopeFirst_AndPriorityAndNotesUnderMoreOptions()
    {
        var cut = RenderCaseCreate();

        foreach (var visible in new[] { "#title", "#description", "#meetingDate", "#scope-whole" })
        {
            Assert.False(IsInsideMoreOptions(cut, visible), visible);
        }

        Assert.True(IsInsideMoreOptions(cut, "#priority"));
        Assert.True(IsInsideMoreOptions(cut, "#notes"));
        Assert.False(IsMoreOptionsOpen(cut));
        Assert.Equal("Prioritet · Merknader", cut.Find("details.disclosure .disclosure-hint").TextContent);
    }

    [Fact]
    public void CaseForm_ExplainsWhatABoardCaseIs_AndTheMeetingDateIsOnlyARegisteredDate()
    {
        var cut = RenderCaseCreate();

        Assert.Contains("En styresak er noe styret skal behandle eller avgjøre.", cut.FindByTestId("case-explanation").TextContent);
        Assert.Equal("/oppgaver/ny", cut.FindByTestId("case-explanation").QuerySelector("a")!.GetAttribute("href"));
        Assert.Equal("meetingDate", cut.FindFieldByLabel("Registrert møtedato").Id);
        var help = cut.Find("#meetingDate-help").TextContent;
        Assert.Contains("bare en registrert dato", help);
        Assert.Contains("ikke en innkalling", help);
    }

    [Fact]
    public async Task CaseForm_ScopeCanBeTheWholeCooperativeOrAUnit_AndIsStoredLikeBefore()
    {
        var unit = await Service.RegisterAsync("Storgata 1A");
        var cut = RenderCaseCreate();
        cut.Find("#title").Change("Lekkasje");
        cut.Find("#description").Change("Fuktskade i vegg.");
        cut.ChooseHousingUnit(unit.Id);

        cut.Find("form").Submit();

        var stored = Assert.Single(await BoardCases.GetAllAsync());
        Assert.Equal(unit.Id, stored.HousingUnitId);
        Assert.Equal("Fuktskade i vegg.", stored.Description);
    }

    [Fact]
    public async Task CaseForm_AUnitIsChosenButNoneSelected_AsksForOne()
    {
        await Service.RegisterAsync("Storgata 1A");
        var cut = RenderCaseCreate();
        cut.Find("#title").Change("Lekkasje");
        cut.Find("#scope-unit").Change(true);

        cut.Find("form").Submit();

        Assert.Contains("Velg en boenhet", cut.FindByTestId("scope-error").TextContent);
        Assert.Empty(await BoardCases.GetAllAsync());
        Assert.Equal("Lekkasje", cut.Find("#title").GetAttribute("value"));
    }

    [Fact]
    public void CaseForm_ErrorInNotes_OpensMoreOptionsAndKeepsTheOtherValues()
    {
        var cut = RenderCaseCreate();
        cut.Find("#title").Change("Lekkasje");
        cut.Find("#description").Change("Fuktskade i vegg.");
        cut.Find("#notes").Change(new string('A', BoardCase.MaxNotesLength + 1));

        cut.Find("form").Submit();

        Assert.True(IsMoreOptionsOpen(cut));
        Assert.Contains("Merknader kan ha maks", cut.Find("details.disclosure .validation-message").TextContent);
        Assert.Equal("Lekkasje", cut.Find("#title").GetAttribute("value"));
        Assert.Equal("Fuktskade i vegg.", cut.Find("#description").GetAttribute("value"));
    }

    [Fact]
    public async Task CaseEdit_ExistingNotesAndPriority_AreVisible_AndSavingChangesNothingElse()
    {
        var unit = await Service.RegisterAsync("Storgata 1A");
        var boardCase = await BoardCases.RegisterAsync(new BoardCaseDetails
        {
            Title = "Lekkasje",
            Description = "Fuktskade.",
            Priority = BoardCasePriority.High,
            MeetingDate = new DateOnly(2026, 12, 3),
            HousingUnitId = unit.Id,
            Notes = "Fiktive merknader."
        });
        var before = (await BoardCases.GetByIdAsync(boardCase.Id))!.ToDetails();

        var cut = RenderCaseEdit(boardCase.Id);

        Assert.True(IsMoreOptionsOpen(cut));
        Assert.Equal(unit.Id.ToString(), cut.SelectedHousingUnit());
        cut.Find("form").Submit();

        Assert.Equal(before, (await BoardCases.GetByIdAsync(boardCase.Id))!.ToDetails());
    }

    // Housing unit form

    [Fact]
    public void UnitForm_FirstRegistration_ShowsAddressAndOwnerFirst_AndTheRestUnderMoreOptions()
    {
        var cut = Render<HousingUnitCreate>();

        foreach (var visible in new[] { "#address", "#ownerName", "#ownerPhoneNumber", "#ownerEmail" })
        {
            Assert.False(IsInsideMoreOptions(cut, visible), visible);
        }

        foreach (var hidden in new[] { "#unitNumber", "#sectionNumber", "#housingType", "#areaSquareMeters", "#notes" })
        {
            Assert.True(IsInsideMoreOptions(cut, hidden), hidden);
        }

        Assert.False(IsMoreOptionsOpen(cut));
        Assert.Equal("Ny boenhet", cut.FindByTestId("page-title").TextContent);
        Assert.Contains("(valgfritt)", cut.Find("form").TextContent);
    }

    [Fact]
    public async Task UnitForm_ErrorInAFieldUnderMoreOptions_OpensItAndKeepsTheOwner()
    {
        var cut = Render<HousingUnitCreate>();
        cut.Find("#address").Change("Storgata 1A");
        cut.Find("#ownerName").Change("Test Eier");
        cut.Find("#unitNumber").Change(new string('H', HousingUnit.MaxUnitNumberLength + 1));

        cut.Find("form").Submit();

        Assert.True(IsMoreOptionsOpen(cut));
        Assert.Contains("Bolignummer kan ha maks", cut.Find("details.disclosure .validation-message").TextContent);
        Assert.Equal("Storgata 1A", cut.Find("#address").GetAttribute("value"));
        Assert.Equal("Test Eier", cut.Find("#ownerName").GetAttribute("value"));
        Assert.Empty(await Service.GetAllAsync());
    }

    [Fact]
    public async Task UnitForm_Edit_ExistingValuesUnderMoreOptions_AreVisible_AndSavingChangesNothing()
    {
        var unit = await Service.RegisterAsync(new HousingUnitDetails
        {
            Address = "Storgata 1A",
            UnitNumber = "H0101",
            SectionNumber = 12,
            HousingType = HousingType.Duplex,
            AreaSquareMeters = 85.5m,
            OwnerName = "Test Eier",
            Notes = "Fiktive testdata."
        });
        var before = HousingUnitFormModel.From((await Service.GetByIdAsync(unit.Id))!).ToDetails();

        var cut = RenderUnitEdit(unit.Id);

        Assert.True(IsMoreOptionsOpen(cut));
        Assert.Equal("H0101", cut.Find("#unitNumber").GetAttribute("value"));
        cut.Find("form").Submit();

        Assert.Equal(before, HousingUnitFormModel.From((await Service.GetByIdAsync(unit.Id))!).ToDetails());
    }

    [Fact]
    public async Task UnitForm_Edit_OnlyAddressRegistered_KeepsMoreOptionsClosed()
    {
        var unit = await Service.RegisterAsync("Storgata 1A");

        var cut = RenderUnitEdit(unit.Id);

        Assert.False(IsMoreOptionsOpen(cut));
    }
}
