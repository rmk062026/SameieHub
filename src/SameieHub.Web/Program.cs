using SameieHub.Domain;
using SameieHub.Infrastructure;
using SameieHub.Infrastructure.Demo;
using SameieHub.Infrastructure.Persistence;
using SameieHub.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// The demo command (scripts/demo.ps1) is the only way demo data is created. It only runs against the dedicated
// demo database, so it can never fill the ordinary database or any other file by accident.
var seedDemo = args.Contains("--seed-demo");
if (seedDemo && !DemoDatabaseSeeder.IsDemoDatabasePath(builder.Configuration["Database:Path"], DemoDatabaseLocation.GetPath()))
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
builder.Services.AddSingleton(TimeProvider.System);

var app = builder.Build();

await app.Services.MigrateSameieHubDatabaseAsync();

if (seedDemo)
{
    return await RunDemoSeedingAsync(app.Services);
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
    var seeder = new DemoDatabaseSeeder(
        scope.ServiceProvider.GetRequiredService<IHousingUnitRepository>(),
        scope.ServiceProvider.GetRequiredService<IBoardCaseRepository>(),
        scope.ServiceProvider.GetRequiredService<ITaskItemRepository>(),
        TimeProvider.System);

    try
    {
        var result = await seeder.SeedAsync();

        Console.WriteLine(result == DemoSeedResult.Seeded
            ? "Demo data created."
            : "The demo data is already in the database. Nothing was changed.");

        return 0;
    }
    catch (Exception exception) when (exception is DemoDatabaseNotEmptyException or DemoDatabaseIncompleteException or AggregateException)
    {
        Console.Error.WriteLine(exception.Message);

        return 1;
    }
}
