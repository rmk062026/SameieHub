using SameieHub.Domain;

namespace SameieHub.Web.Components.Pages.HousingUnits;

/// <summary>
/// Norwegian messages for the rules enforced by <see cref="HousingUnit"/>.
/// The keys are the property names the domain reports in <see cref="ArgumentException.ParamName"/>.
/// </summary>
public static class HousingUnitFieldMessages
{
    private const string PhoneMessage =
        "Telefonnummer kan bare inneholde tall, mellomrom, +, - og parenteser, og må ha minst 5 siffer.";

    private const string EmailMessage = "Skriv inn en gyldig e-postadresse, for eksempel navn@example.no.";

    private static readonly string OwnerNameMessage = $"Navn kan ha maks {HousingUnit.MaxOwnerNameLength} tegn.";

    private static readonly Dictionary<string, string> Messages = new()
    {
        ["Address"] = $"Adressen må fylles ut og kan ha maks {HousingUnit.MaxAddressLength} tegn.",
        ["UnitNumber"] = $"Bolignummer kan ha maks {HousingUnit.MaxUnitNumberLength} tegn.",
        ["SectionNumber"] = $"Seksjonsnummer må være et helt tall fra {HousingUnit.MinSectionNumber} til {HousingUnit.MaxSectionNumber}.",
        ["AreaSquareMeters"] = $"Arealet må være større enn 0 og høyst {HousingUnit.MaxAreaSquareMeters} m².",
        ["OwnerName"] = OwnerNameMessage,
        ["OwnerPhoneNumber"] = PhoneMessage,
        ["OwnerEmail"] = EmailMessage,
        ["SecondOwnerName"] = OwnerNameMessage,
        ["SecondOwnerPhoneNumber"] = PhoneMessage,
        ["SecondOwnerEmail"] = EmailMessage,
        ["Notes"] = $"Merknader kan ha maks {HousingUnit.MaxNotesLength} tegn."
    };

    public static bool TryGet(string? fieldName, out string message)
    {
        message = string.Empty;

        return fieldName != null && Messages.TryGetValue(fieldName, out message!);
    }
}
