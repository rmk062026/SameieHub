using Bunit;
using Microsoft.Extensions.DependencyInjection;
using SameieHub.Domain;
using SameieHub.Tests.Fakes;

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
    }

    protected FailableHousingUnitRepository Repository { get; } = new();

    protected FakeBoardCaseRepository BoardCaseRepository { get; } = new();

    protected FakeTaskItemRepository TaskRepository { get; } = new();

    protected ManualTimeProvider Clock { get; } = new(new DateTime(2026, 11, 18, 10, 30, 0, DateTimeKind.Utc));

    protected HousingUnitService Service => Services.GetRequiredService<HousingUnitService>();

    protected TaskItemService Tasks => Services.GetRequiredService<TaskItemService>();

    protected BoardCaseService BoardCases => Services.GetRequiredService<BoardCaseService>();

    protected string CurrentPath => new Uri(Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>().Uri).PathAndQuery;
}
