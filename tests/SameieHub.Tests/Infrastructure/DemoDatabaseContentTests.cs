using SameieHub.Domain;

namespace SameieHub.Tests.Infrastructure;

/// <summary>
/// Read-only checks of the content of the seeded Fiktivdalen demo (units, board cases, tasks). They share one
/// seeded database (<see cref="SeededDemoFixture"/>) and never change it. Tests that create, change, reset or
/// break the demo are in <see cref="DemoDatabaseSeederTests"/> and have their own database.
/// </summary>
public class DemoDatabaseContentTests : IClassFixture<SeededDemoFixture>
{
    // The test clock starts at 2026-11-18 10:30 UTC, which is 11:30 on 18 November in Norway.
    private static readonly DateOnly Today = new(2026, 11, 18);

    private readonly SeededDemoFixture _demo;

    public DemoDatabaseContentTests(SeededDemoFixture demo)
    {
        _demo = demo;
    }

    [Fact]
    public async Task Seed_Units_AreActiveWithSectionNumbers1To8AndPlausibleAreas()
    {
        var units = (await _demo.UnitsAsync()).OrderBy(unit => unit.Id).ToList();
        Assert.All(units, unit => Assert.True(unit.IsActive));
        Assert.Equal([1, 2, 3, 4, 5, 6, 7, 8], units.Select(unit => unit.SectionNumber!.Value));
        Assert.All(units, unit => Assert.InRange(unit.AreaSquareMeters!.Value, 95m, 145m));
        Assert.All(units, unit => Assert.Equal(HousingType.Duplex, unit.HousingType));
        Assert.All(units, unit => Assert.NotNull(unit.UnitNumber));
    }

    [Fact]
    public async Task Seed_Owners_AreMixedAndFictional()
    {
        var units = await _demo.UnitsAsync();
        Assert.True(units.Count(unit => unit.SecondOwnerName != null) >= 3);
        Assert.True(units.Count(unit => unit.SecondOwnerName == null) >= 3);
        Assert.Contains(units, unit => unit.OwnerPhoneNumber == null);
        Assert.Contains(units, unit => unit.OwnerEmail == null);
        Assert.Contains(units, unit => unit.OwnerPhoneNumber != null && unit.OwnerEmail != null);
        Assert.All(units, unit => Assert.NotNull(unit.OwnerName));
    }

    [Fact]
    public async Task Seed_ContactDetails_AreClearlyFictional()
    {
        var units = await _demo.UnitsAsync();
        var emails = units.SelectMany(unit => new[] { unit.OwnerEmail, unit.SecondOwnerEmail }).OfType<string>().ToList();
        var phones = units.SelectMany(unit => new[] { unit.OwnerPhoneNumber, unit.SecondOwnerPhoneNumber }).OfType<string>().ToList();
        Assert.NotEmpty(emails);
        Assert.All(emails, email => Assert.EndsWith("@example.com", email));
        Assert.NotEmpty(phones);
        Assert.All(phones, phone => Assert.StartsWith("+47 000", phone));
    }

    [Fact]
    public async Task Seed_EveryUnit_HasCarportInTheNotesAndTheChargersAreModelledSeparately()
    {
        var units = await _demo.UnitsAsync();
        Assert.All(units, unit =>
        {
            Assert.Contains("Carport", unit.Notes);
            Assert.DoesNotContain("Elbillader", unit.Notes);
        });
        Assert.Equal(8, (await _demo.ChargersAsync()).Count);
    }

    [Fact]
    public async Task Seed_CreatesTwelveBoardCasesInAllFourStatuses()
    {
        var cases = await _demo.BoardCasesAsync();
        Assert.Equal(12, cases.Count);
        foreach (var status in Enum.GetValues<BoardCaseStatus>())
        {
            Assert.Contains(cases, boardCase => boardCase.Status == status);
        }
    }

    [Fact]
    public async Task Seed_DecidedAndClosedCases_HaveDecisionsAndSomeHaveDecisionDates()
    {
        var decided = (await _demo.BoardCasesAsync()).Where(boardCase => boardCase.Status is BoardCaseStatus.Decided or BoardCaseStatus.Closed).ToList();
        Assert.True(decided.Count >= 4);
        Assert.All(decided, boardCase => Assert.NotNull(boardCase.Decision));
        Assert.Contains(decided, boardCase => boardCase.DecisionDate != null);
        Assert.Contains(decided, boardCase => boardCase.DecisionDate == null);
        Assert.All(decided.Where(boardCase => boardCase.DecisionDate != null), boardCase => Assert.True(boardCase.DecisionDate <= Today));
    }

    [Fact]
    public async Task Seed_BoardCases_HavePastAndUpcomingMeetingsAndMixedLinks()
    {
        var cases = await _demo.BoardCasesAsync();
        Assert.Contains(cases, boardCase => boardCase.MeetingDate < Today);
        Assert.Contains(cases, boardCase => boardCase.MeetingDate >= Today);
        Assert.Contains(cases, boardCase => boardCase.HousingUnitId == null);
        Assert.Contains(cases, boardCase => boardCase.HousingUnitId != null);
        Assert.Equal(cases.Count, cases.Select(boardCase => boardCase.Title).Distinct().Count());
        Assert.All(cases, boardCase => Assert.NotNull(boardCase.Description));
    }

    [Fact]
    public async Task Seed_ContainsTheTopicsFromTheRequirements()
    {
        var titles = (await _demo.BoardCasesAsync()).Select(boardCase => boardCase.Title).ToList();
        foreach (var topic in new[] { "uteområde", "elbilladeanlegget", "carporter", "fellesbelysning", "årlig sameiemøte", "gjerde", "Snørydding", "drenering", "gjesteparkering", "vedlikeholdsplan", "lekkasje", "kapasitet" })
        {
            Assert.Contains(titles, title => title.Contains(topic, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public async Task Seed_CreatesTwentyTasksWithAllStatusesAndCategories()
    {
        var tasks = await _demo.TasksAsync();
        Assert.Equal(20, tasks.Count);
        foreach (var status in Enum.GetValues<TaskItemStatus>())
        {
            Assert.Contains(tasks, task => task.Status == status);
        }

        foreach (var category in Enum.GetValues<TaskItemCategory>())
        {
            Assert.Contains(tasks, task => task.Category == category);
        }

        foreach (var priority in Enum.GetValues<TaskItemPriority>())
        {
            Assert.Contains(tasks, task => task.Priority == priority);
        }
    }

    [Fact]
    public async Task Seed_Tasks_HaveTheRequestedSpreadOfDueDates()
    {
        var tasks = await _demo.TasksAsync();
        var open = tasks.Where(task => task.Status != TaskItemStatus.Completed).ToList();
        Assert.True(open.Count(task => task.IsOverdue(Today)) >= 3);
        Assert.True(open.Count(task => task.DueDate == Today) >= 2);
        Assert.True(open.Count(task => task.DueDate > Today && task.DueDate <= Today.AddDays(7)) >= 3);
        Assert.True(open.Count(task => task.DueDate >= Today.AddDays(30) && task.DueDate <= Today.AddDays(60)) >= 2);
        Assert.True(tasks.Count(task => task.DueDate == null) >= 2);
    }

    [Fact]
    public async Task Seed_CompletedTasks_HaveCompletionTimesInThePast()
    {
        var completed = (await _demo.TasksAsync()).Where(task => task.Status == TaskItemStatus.Completed).ToList();
        Assert.True(completed.Count >= 4);
        var now = _demo.Now;
        Assert.All(completed, task =>
        {
            Assert.NotNull(task.CompletedAt);
            Assert.True(task.CompletedAt <= now);
            Assert.True(task.CompletedAt >= task.CreatedAt);
        });
        Assert.All((await _demo.TasksAsync()).Where(task => task.Status != TaskItemStatus.Completed), task => Assert.Null(task.CompletedAt));
    }

    [Fact]
    public async Task Seed_Tasks_HaveAllKindsOfLinksAndAssignees()
    {
        var tasks = await _demo.TasksAsync();
        Assert.Contains(tasks, task => task.HousingUnitId != null && task.BoardCaseId == null);
        Assert.Contains(tasks, task => task.HousingUnitId == null && task.BoardCaseId != null);
        Assert.Contains(tasks, task => task.HousingUnitId != null && task.BoardCaseId != null);
        Assert.Contains(tasks, task => task.HousingUnitId == null && task.BoardCaseId == null);
        Assert.Contains(tasks, task => task.AssignedTo != null);
        Assert.Contains(tasks, task => task.AssignedTo == null);
    }

    [Fact]
    public async Task Seed_AllLinks_RefersToExistingUnitsAndCases()
    {
        var unitIds = (await _demo.UnitsAsync()).Select(unit => unit.Id).ToHashSet();
        var caseIds = (await _demo.BoardCasesAsync()).Select(boardCase => boardCase.Id).ToHashSet();
        Assert.All(await _demo.BoardCasesAsync(), boardCase => Assert.True(boardCase.HousingUnitId == null || unitIds.Contains(boardCase.HousingUnitId.Value)));
        Assert.All(await _demo.TasksAsync(), task =>
        {
            Assert.True(task.HousingUnitId == null || unitIds.Contains(task.HousingUnitId.Value));
            Assert.True(task.BoardCaseId == null || caseIds.Contains(task.BoardCaseId.Value));
        });
    }

    [Fact]
    public async Task Seed_ChargerTaskIsLinkedToTheChargerCaseThatConcernsTheWholeCooperative()
    {
        var cases = await _demo.BoardCasesAsync();
        var chargerCase = cases.Single(boardCase => boardCase.Title.Contains("Service på elbilladeanlegget"));
        Assert.Null(chargerCase.HousingUnitId);
        Assert.Contains(await _demo.TasksAsync(), task => task.BoardCaseId == chargerCase.Id);
    }

    [Fact]
    public async Task Seed_TimestampsAreSpreadOverTimeAndNeverInTheFuture()
    {
        var now = _demo.Now;
        var tasks = await _demo.TasksAsync();
        var cases = await _demo.BoardCasesAsync();
        Assert.All(tasks, task => Assert.True(task.CreatedAt <= now && task.UpdatedAt <= now));
        Assert.All(cases, boardCase => Assert.True(boardCase.CreatedAt <= now && boardCase.UpdatedAt <= now));
        Assert.True(tasks.Select(task => task.CreatedAt.Date).Distinct().Count() > 5);
    }

    [Fact]
    public async Task Seed_GivesTheDashboardRealisticNumbers()
    {
        var overview = DashboardOverview.Create(
            await _demo.UnitsAsync(),
            await _demo.BoardCasesAsync(),
            await _demo.TasksAsync(),
            Today);

        Assert.Equal(8, overview.ActiveHousingUnits);
        Assert.Equal(10, overview.OpenBoardCases);
        Assert.True(overview.OverdueTasks >= 3);
        Assert.Equal(DashboardOverview.MaxFollowUpTasks, overview.FollowUpTasks.Count);
        Assert.NotEmpty(overview.UpcomingDates);
    }
}
