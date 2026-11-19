using SameieHub.Domain;
using SameieHub.Infrastructure;
using SameieHub.Infrastructure.Demo;
using SameieHub.Infrastructure.Persistence;

namespace SameieHub.Tests.Infrastructure;

public class DemoDatabaseSeederTests : IAsyncLifetime
{
    // The test clock starts at 2026-11-18 10:30 UTC, which is 11:30 on 18 November in Norway.
    private static readonly DateOnly Today = new(2026, 11, 18);

    private SqliteTestDatabase _database = null!;

    public async Task InitializeAsync()
    {
        _database = await SqliteTestDatabase.CreateAsync();
    }

    public async Task DisposeAsync()
    {
        await _database.DisposeAsync();
    }

    private DemoDatabaseSeeder NewSeeder()
    {
        return new DemoDatabaseSeeder(_database.Repository, _database.BoardCaseRepository, _database.TaskRepository, _database.Clock);
    }

    private async Task<DemoSeedResult> SeedAsync()
    {
        return await NewSeeder().SeedAsync();
    }

    // Housing units

    [Fact]
    public async Task Seed_EmptyDatabase_CreatesExactlyTheEightFiktivdalenUnits()
    {
        var result = await SeedAsync();

        Assert.Equal(DemoSeedResult.Seeded, result);
        var units = await _database.Service.GetAllAsync();
        Assert.Equal(
            ["Fiktivdalen 10A", "Fiktivdalen 10B", "Fiktivdalen 12A", "Fiktivdalen 12B", "Fiktivdalen 14A", "Fiktivdalen 14B", "Fiktivdalen 16A", "Fiktivdalen 16B"],
            units.OrderBy(unit => unit.Id).Select(unit => unit.Address));
    }

    [Fact]
    public async Task Seed_Units_AreActiveWithSectionNumbers1To8AndPlausibleAreas()
    {
        await SeedAsync();

        var units = (await _database.Service.GetAllAsync()).OrderBy(unit => unit.Id).ToList();
        Assert.All(units, unit => Assert.True(unit.IsActive));
        Assert.Equal([1, 2, 3, 4, 5, 6, 7, 8], units.Select(unit => unit.SectionNumber!.Value));
        Assert.All(units, unit => Assert.InRange(unit.AreaSquareMeters!.Value, 95m, 145m));
        Assert.All(units, unit => Assert.Equal(HousingType.Duplex, unit.HousingType));
        Assert.All(units, unit => Assert.NotNull(unit.UnitNumber));
    }

    [Fact]
    public async Task Seed_Owners_AreMixedAndFictional()
    {
        await SeedAsync();

        var units = await _database.Service.GetAllAsync();
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
        await SeedAsync();

        var units = await _database.Service.GetAllAsync();
        var emails = units.SelectMany(unit => new[] { unit.OwnerEmail, unit.SecondOwnerEmail }).OfType<string>().ToList();
        var phones = units.SelectMany(unit => new[] { unit.OwnerPhoneNumber, unit.SecondOwnerPhoneNumber }).OfType<string>().ToList();
        Assert.NotEmpty(emails);
        Assert.All(emails, email => Assert.EndsWith("@example.com", email));
        Assert.NotEmpty(phones);
        Assert.All(phones, phone => Assert.StartsWith("+47 000", phone));
    }

    [Fact]
    public async Task Seed_EveryUnit_HasCarportAndChargerInTheNotes()
    {
        await SeedAsync();

        var units = await _database.Service.GetAllAsync();
        Assert.All(units, unit =>
        {
            Assert.Contains("Carport", unit.Notes);
            Assert.Contains("Elbillader", unit.Notes);
        });
        Assert.Contains(units, unit => unit.Notes!.Contains("i bruk"));
        Assert.Contains(units, unit => unit.Notes!.Contains("ikke i bruk"));
        Assert.Contains(units, unit => unit.Notes!.Contains("midlertidig deaktivert"));
        Assert.Contains(units, unit => unit.Notes!.Contains("Ingen elbil"));
    }

    // Board cases

    [Fact]
    public async Task Seed_CreatesTwelveBoardCasesInAllFourStatuses()
    {
        await SeedAsync();

        var cases = await _database.BoardCases.GetAllAsync();
        Assert.Equal(12, cases.Count);
        foreach (var status in Enum.GetValues<BoardCaseStatus>())
        {
            Assert.Contains(cases, boardCase => boardCase.Status == status);
        }
    }

    [Fact]
    public async Task Seed_DecidedAndClosedCases_HaveDecisionsAndSomeHaveDecisionDates()
    {
        await SeedAsync();

        var decided = (await _database.BoardCases.GetAllAsync()).Where(boardCase => boardCase.Status is BoardCaseStatus.Decided or BoardCaseStatus.Closed).ToList();
        Assert.True(decided.Count >= 4);
        Assert.All(decided, boardCase => Assert.NotNull(boardCase.Decision));
        Assert.Contains(decided, boardCase => boardCase.DecisionDate != null);
        Assert.Contains(decided, boardCase => boardCase.DecisionDate == null);
        Assert.All(decided.Where(boardCase => boardCase.DecisionDate != null), boardCase => Assert.True(boardCase.DecisionDate <= Today));
    }

    [Fact]
    public async Task Seed_BoardCases_HavePastAndUpcomingMeetingsAndMixedLinks()
    {
        await SeedAsync();

        var cases = await _database.BoardCases.GetAllAsync();
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
        await SeedAsync();

        var titles = (await _database.BoardCases.GetAllAsync()).Select(boardCase => boardCase.Title).ToList();
        foreach (var topic in new[] { "uteområde", "elbilladeanlegget", "carporter", "fellesbelysning", "årlig sameiemøte", "gjerde", "Snørydding", "drenering", "gjesteparkering", "vedlikeholdsplan", "lekkasje", "kapasitet" })
        {
            Assert.Contains(titles, title => title.Contains(topic, StringComparison.OrdinalIgnoreCase));
        }
    }

    // Tasks

    [Fact]
    public async Task Seed_CreatesTwentyTasksWithAllStatusesAndCategories()
    {
        await SeedAsync();

        var tasks = await _database.Tasks.GetAllAsync();
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
        await SeedAsync();

        var tasks = await _database.Tasks.GetAllAsync();
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
        await SeedAsync();

        var completed = (await _database.Tasks.GetAllAsync()).Where(task => task.Status == TaskItemStatus.Completed).ToList();
        Assert.True(completed.Count >= 4);
        var now = _database.Clock.GetUtcNow().UtcDateTime;
        Assert.All(completed, task =>
        {
            Assert.NotNull(task.CompletedAt);
            Assert.True(task.CompletedAt <= now);
            Assert.True(task.CompletedAt >= task.CreatedAt);
        });
        Assert.All((await _database.Tasks.GetAllAsync()).Where(task => task.Status != TaskItemStatus.Completed), task => Assert.Null(task.CompletedAt));
    }

    [Fact]
    public async Task Seed_Tasks_HaveAllKindsOfLinksAndAssignees()
    {
        await SeedAsync();

        var tasks = await _database.Tasks.GetAllAsync();
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
        await SeedAsync();

        var unitIds = (await _database.Service.GetAllAsync()).Select(unit => unit.Id).ToHashSet();
        var caseIds = (await _database.BoardCases.GetAllAsync()).Select(boardCase => boardCase.Id).ToHashSet();
        Assert.All(await _database.BoardCases.GetAllAsync(), boardCase => Assert.True(boardCase.HousingUnitId == null || unitIds.Contains(boardCase.HousingUnitId.Value)));
        Assert.All(await _database.Tasks.GetAllAsync(), task =>
        {
            Assert.True(task.HousingUnitId == null || unitIds.Contains(task.HousingUnitId.Value));
            Assert.True(task.BoardCaseId == null || caseIds.Contains(task.BoardCaseId.Value));
        });
    }

    [Fact]
    public async Task Seed_ChargerTaskIsLinkedToTheChargerCaseThatConcernsTheWholeCooperative()
    {
        await SeedAsync();

        var cases = await _database.BoardCases.GetAllAsync();
        var chargerCase = cases.Single(boardCase => boardCase.Title.Contains("Service på elbilladeanlegget"));
        Assert.Null(chargerCase.HousingUnitId);
        Assert.Contains(await _database.Tasks.GetAllAsync(), task => task.BoardCaseId == chargerCase.Id);
    }

    [Fact]
    public async Task Seed_TimestampsAreSpreadOverTimeAndNeverInTheFuture()
    {
        await SeedAsync();

        var now = _database.Clock.GetUtcNow().UtcDateTime;
        var tasks = await _database.Tasks.GetAllAsync();
        var cases = await _database.BoardCases.GetAllAsync();
        Assert.All(tasks, task => Assert.True(task.CreatedAt <= now && task.UpdatedAt <= now));
        Assert.All(cases, boardCase => Assert.True(boardCase.CreatedAt <= now && boardCase.UpdatedAt <= now));
        Assert.True(tasks.Select(task => task.CreatedAt.Date).Distinct().Count() > 5);
    }

    [Fact]
    public async Task Seed_DataSurvivesRestart()
    {
        await SeedAsync();

        await _database.RestartAsync();

        Assert.Equal(8, (await _database.Service.GetAllAsync()).Count);
        Assert.Equal(12, (await _database.BoardCases.GetAllAsync()).Count);
        Assert.Equal(20, (await _database.Tasks.GetAllAsync()).Count);
    }

    [Fact]
    public async Task Seed_GivesTheDashboardRealisticNumbers()
    {
        await SeedAsync();

        var overview = DashboardOverview.Create(
            await _database.Service.GetAllAsync(),
            await _database.BoardCases.GetAllAsync(),
            await _database.Tasks.GetAllAsync(),
            Today);

        Assert.Equal(8, overview.ActiveHousingUnits);
        Assert.Equal(10, overview.OpenBoardCases);
        Assert.True(overview.OverdueTasks >= 3);
        Assert.Equal(DashboardOverview.MaxFollowUpTasks, overview.FollowUpTasks.Count);
        Assert.NotEmpty(overview.UpcomingDates);
    }

    // Safety

    [Fact]
    public async Task Seed_Twice_DoesNotDuplicateAnything()
    {
        await SeedAsync();

        var second = await SeedAsync();

        Assert.Equal(DemoSeedResult.AlreadySeeded, second);
        Assert.Equal(8, (await _database.Service.GetAllAsync()).Count);
        Assert.Equal(12, (await _database.BoardCases.GetAllAsync()).Count);
        Assert.Equal(20, (await _database.Tasks.GetAllAsync()).Count);
    }

    [Fact]
    public async Task Seed_DatabaseWithUserUnit_IsNotChanged()
    {
        await _database.Service.RegisterAsync("Storgata 1A");

        await Assert.ThrowsAsync<DemoDatabaseNotEmptyException>(SeedAsync);

        Assert.Equal("Storgata 1A", Assert.Single(await _database.Service.GetAllAsync()).Address);
        Assert.Empty(await _database.BoardCases.GetAllAsync());
        Assert.Empty(await _database.Tasks.GetAllAsync());
    }

    [Fact]
    public async Task Seed_DatabaseWithOnlyAUserBoardCaseOrTask_IsNotChanged()
    {
        await _database.BoardCases.RegisterAsync(new BoardCaseDetails { Title = "Egen sak" });

        await Assert.ThrowsAsync<DemoDatabaseNotEmptyException>(SeedAsync);

        Assert.Empty(await _database.Service.GetAllAsync());
        Assert.Single(await _database.BoardCases.GetAllAsync());

        await _database.Tasks.RegisterAsync(new TaskItemDetails { Title = "Egen oppgave" });
        await Assert.ThrowsAsync<DemoDatabaseNotEmptyException>(SeedAsync);
        Assert.Single(await _database.Tasks.GetAllAsync());
    }

    [Fact]
    public async Task Seed_DemoDataMixedWithUserData_IsNotTouched()
    {
        await SeedAsync();
        await _database.Service.RegisterAsync("Storgata 1A");

        await Assert.ThrowsAsync<DemoDatabaseNotEmptyException>(SeedAsync);

        Assert.Equal(9, (await _database.Service.GetAllAsync()).Count);
        Assert.Equal(20, (await _database.Tasks.GetAllAsync()).Count);
    }

    [Fact]
    public async Task NormalStartup_MigratesButDoesNotSeedAnything()
    {
        var provider = _database.DatabasePath;
        await _database.RestartAsync();

        Assert.True(File.Exists(provider));
        Assert.Empty(await _database.Service.GetAllAsync());
        Assert.Empty(await _database.BoardCases.GetAllAsync());
        Assert.Empty(await _database.Tasks.GetAllAsync());
    }

    // The dedicated demo database path

    private const string DemoPath = @"C:\Users\test\AppData\Local\SameieHubDemo\sameiehub-demo.db";

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData(@"C:\Users\test\AppData\Local\SameieHub\sameiehub.db", false)]
    [InlineData(@"C:\Users\test\AppData\Local\SameieHubDemo\other.db", false)]
    [InlineData(@"C:\Users\test\AppData\Local\SameieHubDemo\sub\sameiehub-demo.db", false)]
    [InlineData(@"C:\Users\test\AppData\Local\SameieHubDemo2\sameiehub-demo.db", false)]
    [InlineData(@"D:\Temp\my-database.db", false)]
    [InlineData(@"sameiehub-demo.db", false)]
    [InlineData(@"C:\Users\test\AppData\Local\SameieHubDemo\sameiehub-demo.db", true)]
    [InlineData(@"c:\USERS\test\appdata\local\sameiehubdemo\SAMEIEHUB-DEMO.DB", true)]
    [InlineData(@"C:\Users\test\AppData\Local\SameieHub\..\SameieHubDemo\.\sameiehub-demo.db", true)]
    public void IsDemoDatabasePath_AcceptsOnlyTheDedicatedDemoDatabaseAfterNormalizing(string? path, bool expected)
    {
        Assert.Equal(expected, DemoDatabaseSeeder.IsDemoDatabasePath(path, DemoPath));
    }

    [Fact]
    public void IsDemoDatabasePath_OrdinaryDatabaseIsNeverAccepted()
    {
        var ordinary = DatabaseLocation.GetDefaultPath();

        Assert.False(DemoDatabaseSeeder.IsDemoDatabasePath(ordinary, DemoDatabaseLocation.GetPath()));
    }

    [Fact]
    public void DemoDatabaseLocation_IsADifferentFileInADifferentFolderThanTheOrdinaryDatabase()
    {
        var demo = DemoDatabaseLocation.GetPath();
        var ordinary = DatabaseLocation.GetDefaultPath();

        Assert.NotEqual(Path.GetDirectoryName(ordinary), Path.GetDirectoryName(demo));
        Assert.EndsWith(Path.Combine("SameieHubDemo", "sameiehub-demo.db"), demo);
        Assert.True(DemoDatabaseSeeder.IsDemoDatabasePath(demo, demo));
    }

    // A complete demo, a partly created demo and other data are told apart

    private async Task RegisterDemoUnitsAsync(int count)
    {
        for (var number = 1; number <= count; number++)
        {
            await _database.Service.RegisterAsync($"Fiktivdalen {number}A");
        }
    }

    private async Task RegisterCasesAsync(int count)
    {
        for (var number = 1; number <= count; number++)
        {
            await _database.BoardCases.RegisterAsync(new BoardCaseDetails { Title = $"Sak {number}" });
        }
    }

    private async Task RegisterTasksAsync(int count)
    {
        for (var number = 1; number <= count; number++)
        {
            await _database.Tasks.RegisterAsync(new TaskItemDetails { Title = $"Oppgave {number}" });
        }
    }

    [Theory]
    [InlineData(1, 0, 0)]
    [InlineData(7, 0, 0)]
    [InlineData(8, 1, 0)]
    [InlineData(8, 11, 0)]
    [InlineData(8, 12, 1)]
    [InlineData(8, 12, 19)]
    public async Task Seed_PartlyCreatedDemo_GivesAClearErrorAndChangesNothing(int units, int cases, int tasks)
    {
        await RegisterDemoUnitsAsync(units);
        await RegisterCasesAsync(cases);
        await RegisterTasksAsync(tasks);

        var exception = await Assert.ThrowsAsync<DemoDatabaseIncompleteException>(SeedAsync);

        Assert.Contains("reset", exception.Message);
        Assert.Equal(units, (await _database.Service.GetAllAsync()).Count);
        Assert.Equal(cases, (await _database.BoardCases.GetAllAsync()).Count);
        Assert.Equal(tasks, (await _database.Tasks.GetAllAsync()).Count);
    }

    [Theory]
    [InlineData(8, 12, 21)]
    [InlineData(8, 13, 20)]
    [InlineData(9, 12, 20)]
    [InlineData(8, 0, 5)]
    [InlineData(0, 3, 0)]
    [InlineData(3, 12, 20)]
    public async Task Seed_CountsThatAreNotACompleteDemoOrAPartlyCreatedOne_AreTreatedAsOtherData(int units, int cases, int tasks)
    {
        await RegisterDemoUnitsAsync(units);
        await RegisterCasesAsync(cases);
        await RegisterTasksAsync(tasks);

        await Assert.ThrowsAsync<DemoDatabaseNotEmptyException>(SeedAsync);
    }

    [Fact]
    public async Task Seed_RightCountsButNotTheDemoContent_IsNotReportedAsComplete()
    {
        await RegisterDemoUnitsAsync(8);
        await RegisterCasesAsync(12);
        await RegisterTasksAsync(20);

        await Assert.ThrowsAsync<DemoDatabaseNotEmptyException>(SeedAsync);
    }

    [Fact]
    public async Task Seed_CompleteDemoWithOneTaskDeleted_IsNotReportedAsComplete()
    {
        await SeedAsync();
        await _database.Tasks.DeleteAsync((await _database.Tasks.GetAllAsync()).First().Id);

        await Assert.ThrowsAnyAsync<InvalidOperationException>(SeedAsync);

        Assert.Equal(19, (await _database.Tasks.GetAllAsync()).Count);
    }

    // Failure while the demo is being created

    [Fact]
    public async Task Seed_FailureInTheMiddle_RemovesWhatWasCreatedSoTheDemoIsNeverHalfDone()
    {
        var failing = new FailingTaskRepository(_database.TaskRepository, failOnAdd: 10);
        var seeder = new DemoDatabaseSeeder(_database.Repository, _database.BoardCaseRepository, failing, _database.Clock);

        await Assert.ThrowsAsync<IOException>(seeder.SeedAsync);

        Assert.Empty(await _database.Service.GetAllAsync());
        Assert.Empty(await _database.BoardCases.GetAllAsync());
        Assert.Empty(await _database.Tasks.GetAllAsync());
    }

    [Fact]
    public async Task Seed_AfterAFailedAttempt_CanBeRunAgainAndCompletes()
    {
        var failing = new FailingTaskRepository(_database.TaskRepository, failOnAdd: 3);
        var seeder = new DemoDatabaseSeeder(_database.Repository, _database.BoardCaseRepository, failing, _database.Clock);
        await Assert.ThrowsAsync<IOException>(seeder.SeedAsync);

        var result = await SeedAsync();

        Assert.Equal(DemoSeedResult.Seeded, result);
        Assert.Equal(20, (await _database.Tasks.GetAllAsync()).Count);
    }

    [Fact]
    public async Task Seed_FailureAndFailedCleanup_LeavesAPartlyCreatedDemoThatIsReportedAsIncomplete()
    {
        var failing = new FailingTaskRepository(_database.TaskRepository, failOnAdd: 5, failOnDelete: true);
        var seeder = new DemoDatabaseSeeder(_database.Repository, _database.BoardCaseRepository, failing, _database.Clock);
        var exception = await Assert.ThrowsAsync<AggregateException>(seeder.SeedAsync);

        Assert.Contains("reset", exception.Message);
        Assert.Equal(2, exception.InnerExceptions.Count);
        await Assert.ThrowsAsync<DemoDatabaseIncompleteException>(SeedAsync);
    }

    private sealed class FailingTaskRepository : ITaskItemRepository
    {
        private readonly ITaskItemRepository _inner;
        private readonly int _failOnAdd;
        private readonly bool _failOnDelete;
        private int _adds;

        public FailingTaskRepository(ITaskItemRepository inner, int failOnAdd, bool failOnDelete = false)
        {
            _inner = inner;
            _failOnAdd = failOnAdd;
            _failOnDelete = failOnDelete;
        }

        public Task AddAsync(TaskItem task)
        {
            _adds++;
            if (_adds == _failOnAdd)
            {
                throw new IOException("Simulated database failure.");
            }

            return _inner.AddAsync(task);
        }

        public Task<TaskItem?> GetByIdAsync(int id) => _inner.GetByIdAsync(id);

        public Task<IReadOnlyList<TaskItem>> GetAllAsync() => _inner.GetAllAsync();

        public Task UpdateAsync(TaskItem task) => _inner.UpdateAsync(task);

        public Task<bool> DeleteAsync(int id)
        {
            if (_failOnDelete)
            {
                throw new IOException("Simulated database failure.");
            }

            return _inner.DeleteAsync(id);
        }
    }
}
