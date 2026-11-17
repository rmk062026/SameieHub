using SameieHub.Domain;

namespace SameieHub.Web.Components.Pages.HousingUnits;

public static class HousingTypeText
{
    public static readonly HousingType[] All = Enum.GetValues<HousingType>();

    public static string ToNorwegian(HousingType housingType)
    {
        return housingType switch
        {
            HousingType.Duplex => "Tomannsbolig",
            HousingType.DetachedHouse => "Enebolig",
            HousingType.TerracedHouse => "Rekkehus",
            HousingType.Apartment => "Leilighet",
            _ => "Annet"
        };
    }
}
