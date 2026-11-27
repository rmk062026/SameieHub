using SameieHub.Domain;
using SameieHub.Infrastructure;
using SameieHub.Web.Components;

namespace SameieHub.Web.Hosting;

/// <summary>
/// The services and the request pipeline of the program, in one place so the Windows start, the development start and the tests build
/// them the same way. <paramref name="windowsHost"/> is the only difference: it adds the access guard and leaves out https redirection.
/// </summary>
internal static class WebAppSetup
{
    public static void AddServices(WebApplicationBuilder builder, string databasePath, bool windowsHost)
    {
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();

        builder.Services.AddSameieHubInfrastructure(databasePath);
        builder.Services.AddScoped<HousingUnitService>();
        builder.Services.AddScoped<BoardCaseService>();
        builder.Services.AddScoped<TaskItemService>();
        builder.Services.AddScoped<ChargingService>();
        builder.Services.AddSingleton(TimeProvider.System);

        // Registered in every start so the pages can ask it, but only the Windows mode lets it stop the program.
        builder.Services.AddSingleton(provider => new ShutdownService(
            provider.GetRequiredService<IHostApplicationLifetime>(),
            provider.GetRequiredService<ILogger<ShutdownService>>(),
            canShutDown: windowsHost,
            canRestart: windowsHost && RestartLaunch.CanStartItself(Environment.ProcessPath),
            MessageDelay,
            hasValidAccess: windowsHost ? provider.GetRequiredService<AccessSession>().HasValidSession : null));

        if (windowsHost)
        {
            builder.Services.AddSingleton<AccessSession>();
        }
    }

    // Long enough for the browser to show "SameieHub avsluttes" before the connection ends, short enough that nobody waits for it.
    private static readonly TimeSpan MessageDelay = TimeSpan.FromMilliseconds(1500);

    public static void UsePipeline(WebApplication app, bool windowsHost)
    {
        if (windowsHost)
        {
            // First of all, before routing and the error pages, so nothing can be reached around it.
            app.UseMiddleware<AccessGuardMiddleware>();
        }

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error", createScopeForErrors: true);
            if (!windowsHost)
            {
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }
        }

        app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
        if (!windowsHost)
        {
            // The Windows mode only has a plain http address on this computer, so there is no https address to redirect to.
            app.UseHttpsRedirection();
        }

        app.UseAntiforgery();

        app.MapStaticAssets();
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();
    }
}
