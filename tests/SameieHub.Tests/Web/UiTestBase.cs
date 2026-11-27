using Bunit;
using Microsoft.Extensions.DependencyInjection;
using SameieHub.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using SameieHub.Tests.Fakes;
using SameieHub.Web.Hosting;

namespace SameieHub.Tests.Web;

public abstract class UiTestBase : BunitContext
{
    protected UiTestBase()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        Services.AddLogging();
        Services.AddSingleton<IHousingUnitRepository>(Repository);
        Services.AddSingleton<HousingUnitService>();
        Services.AddSingleton<IBoardCaseRepository>(BoardCaseRepository);
        Services.AddSingleton<TimeProvider>(Clock);
        Services.AddSingleton<BoardCaseService>();
        Services.AddSingleton<ITaskItemRepository>(TaskRepository);
        Services.AddSingleton<TaskItemService>();
        Services.AddSingleton<IChargingFacilityRepository>(new FakeChargingFacilityRepository(Meters));
        Services.AddSingleton<IEvChargerRepository>(ChargerRepository);
        Services.AddSingleton<IMeterRepository>(Meters);
        Services.AddSingleton<IMeterReadingRepository>(Readings);
        Services.AddSingleton<ChargingService>();
        Services.AddSingleton<SameieHub.Infrastructure.Restore.IDatabaseRestore>(new FakeDatabaseRestore());
        UseShutdown(canShutDown: false);
    }

    protected FakeHostLifetime Lifetime { get; } = new();

    internal ShutdownService Shutdown => Services.GetRequiredService<ShutdownService>();

    /// <summary>
    /// The development start cannot stop the program (the default here, as in <c>dotnet run</c>). The Windows mode can, and can start itself
    /// again. Call this before a component is rendered.
    /// </summary>
    protected void UseShutdown(bool canShutDown, bool canRestart = false)
    {
        Services.AddSingleton(new ShutdownService(Lifetime, NullLogger<ShutdownService>.Instance, canShutDown, canRestart, TimeSpan.Zero));
    }

    protected FakeMeterRepository Meters { get; } = new();

    protected FakeMeterReadingRepository Readings { get; } = new();

    protected FakeEvChargerRepository ChargerRepository => _chargerRepository ??= new FakeEvChargerRepository(Meters);

    private FakeEvChargerRepository? _chargerRepository;

    protected ChargingService Charging => Services.GetRequiredService<ChargingService>();

    protected FailableHousingUnitRepository Repository { get; } = new();

    protected FakeBoardCaseRepository BoardCaseRepository { get; } = new();

    protected FakeTaskItemRepository TaskRepository { get; } = new();

    protected ManualTimeProvider Clock { get; } = new(new DateTime(2026, 11, 18, 10, 30, 0, DateTimeKind.Utc));

    protected HousingUnitService Service => Services.GetRequiredService<HousingUnitService>();

    protected TaskItemService Tasks => Services.GetRequiredService<TaskItemService>();

    protected BoardCaseService BoardCases => Services.GetRequiredService<BoardCaseService>();

    protected string CurrentPath => new Uri(Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>().Uri).PathAndQuery;
}
