using System.ComponentModel.DataAnnotations;
using SameieHub.Domain;

namespace SameieHub.Web.Components.Pages.Charging;

/// <summary>
/// The values bound to the charger form fields. Only «choose a housing unit» and «choose a cabinet» are checked here;
/// all other rules are enforced by <see cref="EvCharger"/> and <see cref="ChargingService"/>.
/// </summary>
public class ChargerFormModel
{
    [Required(ErrorMessage = "Velg boenheten laderen tilhører.")]
    public int? HousingUnitId { get; set; }

    [Required(ErrorMessage = "Velg sikringsskap 1 eller 2.")]
    public int? CabinetNumber { get; set; }

    public string? ChargerNumber { get; set; }

    public string? Manufacturer { get; set; }

    public string? Model { get; set; }

    public ChargerStatus Status { get; set; } = ChargerStatus.InUse;

    /// <summary>"" (not registered), "yes" or "no".</summary>
    public string HasElectricVehicle { get; set; } = string.Empty;

    public string? Notes { get; set; }

    public static ChargerFormModel From(EvCharger charger, int? cabinetNumber)
    {
        return new ChargerFormModel
        {
            HousingUnitId = charger.HousingUnitId,
            CabinetNumber = cabinetNumber,
            ChargerNumber = charger.ChargerNumber,
            Manufacturer = charger.Manufacturer,
            Model = charger.Model,
            Status = charger.Status,
            HasElectricVehicle = charger.HasElectricVehicle switch { true => "yes", false => "no", _ => string.Empty },
            Notes = charger.Notes
        };
    }

    public EvChargerDetails ToDetails()
    {
        return new EvChargerDetails
        {
            ChargerNumber = ChargerNumber,
            Manufacturer = Manufacturer,
            Model = Model,
            Status = Status,
            HasElectricVehicle = HasElectricVehicle switch { "yes" => true, "no" => false, _ => null },
            Notes = Notes
        };
    }
}
