using System.Globalization;
using SameieHub.Domain;
using SameieHub.Web.Components.Shared;

namespace SameieHub.Web.Components.Pages.Charging;

/// <summary>Norwegian texts, number formats and CSS classes for the charging pages.</summary>
public static class ChargingText
{
    public static readonly ChargerStatus[] AllStatuses = Enum.GetValues<ChargerStatus>();

    public static string StatusText(ChargerStatus status)
    {
        return status switch
        {
            ChargerStatus.InUse => "I bruk",
            ChargerStatus.NotInUse => "Ikke i bruk",
            _ => "Midlertidig deaktivert"
        };
    }

    public static string StatusCssClass(ChargerStatus status)
    {
        return status switch
        {
            ChargerStatus.InUse => "status-decided",
            ChargerStatus.NotInUse => "status-closed",
            _ => "status-in-progress"
        };
    }

    public static string VehicleText(bool? hasElectricVehicle)
    {
        return hasElectricVehicle switch
        {
            true => "Ja",
            false => "Nei",
            _ => "Ikke registrert"
        };
    }

    /// <summary>A meter value with a decimal comma and no more decimals than needed, for example "1234,5".</summary>
    public static string Number(decimal value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture).Replace('.', ',');
    }

    public static string Kwh(decimal value) => $"{Number(value)} kWh";

    /// <summary>A difference with a sign, for example "+24 kWh" or "−3,5 kWh".</summary>
    public static string SignedKwh(decimal value)
    {
        var sign = value > 0 ? "+" : value < 0 ? "−" : string.Empty;

        return $"{sign}{Number(Math.Abs(value))} kWh";
    }

    public static string Percent(decimal value)
    {
        var sign = value > 0 ? "+" : value < 0 ? "−" : string.Empty;

        return $"{sign}{Math.Abs(value).ToString("0.0", CultureInfo.InvariantCulture).Replace('.', ',')} %";
    }

    public static string Time(DateTime utc) => NorwegianDateFormat.DateTime(utc);

    public static string Period(DateTime fromUtc, DateTime toUtc) => $"{Time(fromUtc)} – {Time(toUtc)}";

    public static string StateText(MeterConsumptionState state)
    {
        return state switch
        {
            MeterConsumptionState.Documented => "Dokumentert",
            MeterConsumptionState.NoReadings => "Ingen avlesninger",
            MeterConsumptionState.MissingStart => "Mangler avlesning ved start",
            MeterConsumptionState.MissingEnd => "Mangler avlesning ved slutt",
            MeterConsumptionState.MissingBoth => "Mangler avlesning ved start og slutt",
            _ => "Samme avlesning ved start og slutt"
        };
    }

    /// <summary>The name shown for a meter line: the address of the housing unit for a sub meter.</summary>
    public static string MeterLabel(Meter meter, EvCharger? charger, HousingUnit? unit)
    {
        if (meter.IsMain)
        {
            return meter.Name;
        }

        var number = charger?.ChargerNumber;
        var address = unit?.Address ?? meter.Name;

        return number == null ? address : $"{address} (lader {number})";
    }
}
