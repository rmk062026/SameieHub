using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SameieHub.Domain;
using SameieHub.Infrastructure.Persistence;

namespace SameieHub.Tests.Startup;

/// <summary>
/// Starts the real application (<c>Program.cs</c>, the same container, migrations and routes as the program)
/// against a temporary SQLite database. It never touches the ordinary database in AppData.
/// </summary>
public class StartupTests : IClassFixture<StartupFixture>
{
    private readonly StartupFixture _app;

    public StartupTests(StartupFixture app)
    {
        _app = app;
    }

    [Fact]
    public async Task Startup_UsesTheTemporaryDatabaseAndNotTheOrdinaryOne()
    {
        await using var context = await _app.CreateContextAsync();

        var connection = context.Database.GetConnectionString()!;

        Assert.Contains(_app.DatabasePath, connection);
        Assert.DoesNotContain(DatabaseLocation.GetDefaultPath(), connection);
        Assert.True(File.Exists(_app.DatabasePath));
    }

    [Fact]
    public async Task Startup_AppliesAllMigrations()
    {
        await using var context = await _app.CreateContextAsync();

        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        Assert.NotEmpty(await context.Database.GetAppliedMigrationsAsync());
        Assert.Equal(0, await context.HousingUnits.CountAsync());
    }

    [Fact]
    public async Task NormalStartup_CreatesNoDemoOrOtherData()
    {
        await using var context = await _app.CreateContextAsync();

        Assert.Equal(0, await context.HousingUnits.CountAsync());
        Assert.Equal(0, await context.BoardCases.CountAsync());
        Assert.Equal(0, await context.TaskItems.CountAsync());
        Assert.Equal(0, await context.ChargingFacilities.CountAsync());
        Assert.Equal(0, await context.EvChargers.CountAsync());
        Assert.Equal(0, await context.Meters.CountAsync());
        Assert.Equal(0, await context.MeterReadings.CountAsync());
    }

    [Fact]
    public void Startup_RegistersTheServicesAndRepositoriesTheProgramNeeds()
    {
        using var scope = _app.Services.CreateScope();
        var provider = scope.ServiceProvider;

        Assert.NotNull(provider.GetRequiredService<HousingUnitService>());
        Assert.NotNull(provider.GetRequiredService<BoardCaseService>());
        Assert.NotNull(provider.GetRequiredService<TaskItemService>());
        Assert.NotNull(provider.GetRequiredService<ChargingService>());
        Assert.NotNull(provider.GetRequiredService<TimeProvider>());
        Assert.NotNull(provider.GetRequiredService<IHousingUnitRepository>());
        Assert.NotNull(provider.GetRequiredService<IBoardCaseRepository>());
        Assert.NotNull(provider.GetRequiredService<ITaskItemRepository>());
        Assert.NotNull(provider.GetRequiredService<IChargingFacilityRepository>());
        Assert.NotNull(provider.GetRequiredService<IEvChargerRepository>());
        Assert.NotNull(provider.GetRequiredService<IMeterRepository>());
        Assert.NotNull(provider.GetRequiredService<IMeterReadingRepository>());
    }

    /// <summary>
    /// The pages are not rendered on the server (prerendering is off), so a missing registration would only show up
    /// when a person opens the page. This finds every service a page injects and resolves it from the real container.
    /// </summary>
    [Fact]
    public void Startup_EveryServiceInjectedIntoAPageCanBeResolved()
    {
        var injectedTypes = typeof(Program).Assembly.GetTypes()
            .Where(type => typeof(IComponent).IsAssignableFrom(type))
            .SelectMany(type => type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            .Where(property => property.GetCustomAttribute<InjectAttribute>() != null)
            .Select(property => property.PropertyType)
            .Where(type => !IsProvidedByTheBlazorCircuit(type))
            .Distinct()
            .ToList();

        Assert.NotEmpty(injectedTypes);

        using var scope = _app.Services.CreateScope();
        foreach (var type in injectedTypes)
        {
            Assert.True(scope.ServiceProvider.GetService(type) != null, $"{type.Name} is injected into a component but is not registered.");
        }
    }

    [Fact]
    public async Task Startup_BackupServiceMakesAValidBackupOfTheTemporaryDatabase()
    {
        var backup = _app.Services.GetRequiredService<SameieHub.Infrastructure.Backup.IDatabaseBackup>();
        var destination = Path.Combine(Path.GetDirectoryName(_app.DatabasePath)!, "sikkerhetskopi.db");

        var result = await backup.CreateBackupAsync(destination);

        Assert.Equal(destination, result.FilePath);
        Assert.Empty(SameieHub.Infrastructure.Backup.DatabaseBackupValidator.FindProblems(destination));
        _app.AssertOrdinaryDatabaseIsUnchanged();
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/elbillading")]
    public async Task Get_Page_ReturnsTheApplicationShell(string path)
    {
        using var client = _app.CreateClient();

        var response = await client.GetAsync(path);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("text/html", response.Content.Headers.ContentType?.MediaType + "/");
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("blazor.web.js", html);
        Assert.Contains("<html lang=\"nb\">", html);
    }

    [Fact]
    public async Task Get_UnknownAddress_DoesNotBreakTheApplication()
    {
        using var client = _app.CreateClient();

        var response = await client.GetAsync("/finnes-ikke-her");

        Assert.NotEqual(System.Net.HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task Startup_NeverTouchesTheOrdinaryDatabase()
    {
        using var client = _app.CreateClient();
        await client.GetAsync("/");
        await client.GetAsync("/elbillading");

        _app.AssertOrdinaryDatabaseIsUnchanged();
    }

    [Fact]
    public async Task Startup_CarriesOutAPreparedRestoreBeforeTheDatabaseIsOpened()
    {
        var second = new StartupFixture();
        var folder = Path.GetDirectoryName(second.DatabasePath)!;
        Directory.CreateDirectory(folder);
        var backupFile = Path.Combine(folder, "sikkerhetskopi.db");

        await using (var source = await SameieHub.Tests.Infrastructure.SqliteTestDatabase.CreateAsync())
        {
            await source.Service.RegisterAsync("Fiktivdalen 7 (gjenopprettet)");
            await source.StopForResetAsync();
            await new SameieHub.Infrastructure.Backup.SqliteDatabaseBackup(source.DatabasePath, TimeProvider.System, Microsoft.Extensions.Logging.Abstractions.NullLogger<SameieHub.Infrastructure.Backup.SqliteDatabaseBackup>.Instance)
                .CreateBackupAsync(backupFile);
        }

        var restore = new SameieHub.Infrastructure.Restore.SqliteDatabaseRestore(second.DatabasePath, TimeProvider.System, Microsoft.Extensions.Logging.Abstractions.NullLogger<SameieHub.Infrastructure.Restore.SqliteDatabaseRestore>.Instance);
        await restore.PrepareAsync(backupFile);
        Assert.NotNull(restore.GetPending());

        await second.InitializeAsync();
        await using (var context = await second.CreateContextAsync())
        {
            Assert.Equal("Fiktivdalen 7 (gjenopprettet)", (await context.HousingUnits.SingleAsync()).Address);
        }

        Assert.Null(restore.GetPending());
        Assert.Equal(SameieHub.Infrastructure.Restore.RestoreOutcomeKind.Applied, restore.GetLastOutcome()!.Kind);

        await second.DisposeAsync();
    }

    [Fact]
    public async Task Startup_UnresolvedRestore_StopsTheProgramBeforeAnyDatabaseIsCreated()
    {
        var second = new StartupFixture();
        var restoreFolder = Path.Combine(Path.GetDirectoryName(second.DatabasePath)!, "restore");
        Directory.CreateDirectory(restoreFolder);
        var journal = Path.Combine(restoreFolder, "journal.json");
        await File.WriteAllTextAsync(journal, "skadet journal");

        // The test host does not report an early exit of Program.cs as an error, so only the consequences are checked.
        await Record.ExceptionAsync(() => second.InitializeAsync());

        Assert.False(File.Exists(second.DatabasePath), "The program must not start with a new, empty database.");
        Assert.Equal("skadet journal", await File.ReadAllTextAsync(journal));
        await second.DisposeAsync();
    }

    [Fact]
    public async Task Cleanup_StoppingTheApplicationDeletesTheTemporaryDatabaseAndRestoresTheSetting()
    {
        var before = Environment.GetEnvironmentVariable("Database__Path");
        var second = new StartupFixture();
        await second.InitializeAsync();
        var folder = Path.GetDirectoryName(second.DatabasePath)!;
        Assert.True(File.Exists(second.DatabasePath));

        await second.DisposeAsync();

        Assert.False(Directory.Exists(folder));
        Assert.Equal(before, Environment.GetEnvironmentVariable("Database__Path"));
    }

    private static bool IsProvidedByTheBlazorCircuit(Type type)
    {
        // Framework services such as NavigationManager and ILogger<T> are provided by Blazor, not by Program.cs.
        return type.Namespace != null && (type.Namespace.StartsWith("Microsoft.", StringComparison.Ordinal) || type.Namespace.StartsWith("System", StringComparison.Ordinal))
            && type != typeof(TimeProvider);
    }
}

/// <summary>
/// One running application on a temporary database. The location is passed in the same way as when the program is
/// started normally (the setting <c>Database__Path</c>), and the previous value is restored afterwards.
/// </summary>
public sealed class StartupFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "SameieHubStartupTests", Guid.NewGuid().ToString());
    private readonly string? _previousDatabasePath = Environment.GetEnvironmentVariable("Database__Path");
    private readonly FileState _ordinaryDatabaseBefore = FileState.Read(DatabaseLocation.GetDefaultPath());

    public StartupFixture()
    {
        DatabasePath = Path.Combine(_folder, "startup-test.db");
        Environment.SetEnvironmentVariable("Database__Path", DatabasePath);
    }

    public string DatabasePath { get; }

    public Task InitializeAsync()
    {
        // Building the host runs Program.cs: container, migration and routes.
        _ = Services;

        return Task.CompletedTask;
    }

    Task IAsyncLifetime.DisposeAsync()
    {
        return DisposeAsync().AsTask();
    }

    public new async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();

        Environment.SetEnvironmentVariable("Database__Path", _previousDatabasePath);
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }

        AssertOrdinaryDatabaseIsUnchanged();
    }

    public async Task<SameieHubDbContext> CreateContextAsync()
    {
        var factory = Services.GetRequiredService<IDbContextFactory<SameieHubDbContext>>();

        return await factory.CreateDbContextAsync();
    }

    /// <summary>The ordinary database is only looked at (existence, size and time), never opened.</summary>
    public void AssertOrdinaryDatabaseIsUnchanged()
    {
        Assert.Equal(_ordinaryDatabaseBefore, FileState.Read(DatabaseLocation.GetDefaultPath()));
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
    }

    private sealed record FileState(bool Exists, long Length, DateTime LastWriteUtc)
    {
        public static FileState Read(string path)
        {
            var info = new FileInfo(path);

            return info.Exists ? new FileState(true, info.Length, info.LastWriteTimeUtc) : new FileState(false, 0, default);
        }
    }
}
