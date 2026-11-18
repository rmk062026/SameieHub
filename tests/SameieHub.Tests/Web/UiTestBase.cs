using Bunit;
using Microsoft.Extensions.DependencyInjection;
using SameieHub.Domain;
using SameieHub.Tests.Fakes;

namespace SameieHub.Tests.Web;

public abstract class HousingUnitUiTestBase : BunitContext
{
    protected HousingUnitUiTestBase()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        Services.AddLogging();
        Services.AddSingleton<IHousingUnitRepository>(Repository);
        Services.AddSingleton<HousingUnitService>();
    }

    protected FailableHousingUnitRepository Repository { get; } = new();

    protected HousingUnitService Service => Services.GetRequiredService<HousingUnitService>();

    protected string CurrentPath => new Uri(Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>().Uri).PathAndQuery;
}
