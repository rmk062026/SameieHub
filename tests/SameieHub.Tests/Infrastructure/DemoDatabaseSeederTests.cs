using SameieHub.Domain;
using SameieHub.Infrastructure;
using SameieHub.Infrastructure.Demo;
using SameieHub.Infrastructure.Persistence;

namespace SameieHub.Tests.Infrastructure;

public class DemoDatabaseSeederTests : IAsyncLifetime
{
    private SqliteTestDatabase _database = null!;

    public async Task InitializeAsync()
    {
        _database = await SqliteTestDatabase.CreateAsync();
    }

    public async Task DisposeAsync()
    {
        await _database.DisposeAsync();
    }

    private DemoDatabaseSeeder NewSeeder(ITaskItemRepository? tasks = null)
    {
        return new DemoDatabaseSeeder(
            _database.Repository,
            _database.BoardCaseRepository,
            tasks ?? _database.TaskRepository,
            _database.FacilityRepository,
            _database.ChargerRepository,
            _database.MeterRepository,
            _database.ReadingRepository,
            _database.Clock);
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

    // Board cases

    // Tasks

    [Fact]
    public async Task Seed_DataSurvivesRestart()
    {
        await SeedAsync();

        await _database.RestartAsync();

        Assert.Equal(8, (await _database.Service.GetAllAsync()).Count);
        Assert.Equal(12, (await _database.BoardCases.GetAllAsync()).Count);
        Assert.Equal(20, (await _database.Tasks.GetAllAsync()).Count);
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
        var seeder = NewSeeder(failing);

        await Assert.ThrowsAsync<IOException>(seeder.SeedAsync);

        Assert.Empty(await _database.Service.GetAllAsync());
        Assert.Empty(await _database.BoardCases.GetAllAsync());
        Assert.Empty(await _database.Tasks.GetAllAsync());
    }

    [Fact]
    public async Task Seed_AfterAFailedAttempt_CanBeRunAgainAndCompletes()
    {
        var failing = new FailingTaskRepository(_database.TaskRepository, failOnAdd: 3);
        var seeder = NewSeeder(failing);
        await Assert.ThrowsAsync<IOException>(seeder.SeedAsync);

        var result = await SeedAsync();

        Assert.Equal(DemoSeedResult.Seeded, result);
        Assert.Equal(20, (await _database.Tasks.GetAllAsync()).Count);
    }

    [Fact]
    public async Task Seed_FailureAndFailedCleanup_LeavesAPartlyCreatedDemoThatIsReportedAsIncomplete()
    {
        var failing = new FailingTaskRepository(_database.TaskRepository, failOnAdd: 5, failOnDelete: true);
        var seeder = NewSeeder(failing);
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
