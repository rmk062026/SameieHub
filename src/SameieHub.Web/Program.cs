using SameieHub.Domain;
using SameieHub.Infrastructure;
using SameieHub.Infrastructure.Demo;
using SameieHub.Infrastructure.Persistence;
using SameieHub.Infrastructure.Restore;
using SameieHub.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// The demo command (scripts/demo.ps1) is the only way demo data is created. It only runs against the dedicated
// demo database, so it can never fill the ordinary database or any other file by accident.
var seedDemo = args.Contains("--seed-demo");
var checkDemo = args.Contains("--check-demo");
if ((seedDemo || checkDemo) && !DemoDatabaseSeeder.IsDemoDatabasePath(builder.Configuration["Database:Path"], DemoDatabaseLocation.GetPath()))
{
    Console.Error.WriteLine($"The demo command only runs against the demo database. Set Database__Path to {DemoDatabaseLocation.GetPath()}.");
    return 1;
}

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var databasePath = builder.Configuration["Database:Path"] ?? DatabaseLocation.GetDefaultPath();
builder.Services.AddSameieHubInfrastructure(databasePath);
builder.Services.AddScoped<HousingUnitService>();
builder.Services.AddScoped<BoardCaseService>();
builder.Services.AddScoped<TaskItemService>();
builder.Services.AddScoped<ChargingService>();
builder.Services.AddSingleton(TimeProvider.System);

var app = builder.Build();

// A restore that was prepared while the program was running is carried out here, before any connection to the
// database is opened. This is also where an interrupted restore is finished or undone.
var restoreOutcome = app.Services.GetRequiredService<IDatabaseRestore>().ApplyPendingRestore();
if (restoreOutcome.Kind != RestoreOutcomeKind.NothingPending)
{
    app.Logger.LogWarning("Database restore at startup: {Result}.", restoreOutcome.Kind);
}

await app.Services.MigrateSameieHubDatabaseAsync();

if (seedDemo)
{
    return await RunDemoSeedingAsync(app.Services);
}

if (checkDemo)
{
    return await RunDemoCheckAsync(app.Services);
}

// Only the folder and file name are logged, because the full path can contain the Windows user name.
app.Logger.LogInformation(
    "Using database {FolderName}/{FileName}",
    Path.GetFileName(Path.GetDirectoryName(databasePath)),
    Path.GetFileName(databasePath));

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
return 0;

static async Task<int> RunDemoSeedingAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var seeder = CreateDemoSeeder(scope.ServiceProvider);

    try
    {
        var result = await seeder.SeedAsync();

        Console.WriteLine(result == DemoSeedResult.Seeded
            ? "Demo data created."
            : "The demo data is already in the database. Nothing was changed.");

        return 0;
    }
    catch (Exception exception) when (exception is DemoDatabaseNotEmptyException or DemoDatabaseIncompleteException or DemoDatabaseOutdatedException or AggregateException)
    {
        Console.Error.WriteLine(exception.Message);

        return 1;
    }
}

// Only looks at the demo database and changes nothing. Exit code 0: the demo has charging data or is empty,
// 2: an older demo without charging data, 3: partly created or other data.
static async Task<int> RunDemoCheckAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var state = await CreateDemoSeeder(scope.ServiceProvider).InspectAsync();

    switch (state)
    {
        case DemoDatabaseState.OutdatedWithoutCharging:
            Console.WriteLine(new DemoDatabaseOutdatedException().Message);

            return 2;
        case DemoDatabaseState.PartlyCreated:
            Console.WriteLine("The demo database is partly created or has been changed so it no longer matches the demo. Run the demo command 'reset' for a fresh demo.");

            return 3;
        case DemoDatabaseState.OtherData:
            Console.WriteLine("The demo database contains other data than the demo.");

            return 3;
        default:
            return 0;
    }
}

static DemoDatabaseSeeder CreateDemoSeeder(IServiceProvider provider)
{
    return new DemoDatabaseSeeder(
        provider.GetRequiredService<IHousingUnitRepository>(),
        provider.GetRequiredService<IBoardCaseRepository>(),
        provider.GetRequiredService<ITaskItemRepository>(),
        provider.GetRequiredService<IChargingFacilityRepository>(),
        provider.GetRequiredService<IEvChargerRepository>(),
        provider.GetRequiredService<IMeterRepository>(),
        provider.GetRequiredService<IMeterReadingRepository>(),
        TimeProvider.System);
}
