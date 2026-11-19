using SameieHub.Domain;
using SameieHub.Infrastructure;
using SameieHub.Infrastructure.Persistence;
using SameieHub.Web.Components;

var builder = WebApplication.CreateBuilder(args);

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
