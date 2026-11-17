using System.ComponentModel.DataAnnotations;
using SameieHub.Domain;

namespace SameieHub.Web.Components.Pages.HousingUnits;

/// <summary>
/// The values bound to the form fields. Only the required address is checked here;
/// all other rules are enforced by <see cref="HousingUnit"/>.
/// </summary>
public class HousingUnitFormModel
{
    [Required(ErrorMessage = "Adressen må fylles ut.")]
    public string Address { get; set; } = string.Empty;

    public string? UnitNumber { get; set; }

    public int? SectionNumber { get; set; }

    public HousingType? HousingType { get; set; }

    public decimal? AreaSquareMeters { get; set; }

    public string? OwnerName { get; set; }

    public string? OwnerPhoneNumber { get; set; }

    public string? OwnerEmail { get; set; }

    public string? SecondOwnerName { get; set; }

    public string? SecondOwnerPhoneNumber { get; set; }

    public string? SecondOwnerEmail { get; set; }

    public string? Notes { get; set; }

    public bool HasSecondOwnerData =>
        !string.IsNullOrWhiteSpace(SecondOwnerName)
        || !string.IsNullOrWhiteSpace(SecondOwnerPhoneNumber)
        || !string.IsNullOrWhiteSpace(SecondOwnerEmail);

    public static HousingUnitFormModel From(HousingUnit unit)
    {
        return new HousingUnitFormModel
        {
            Address = unit.Address,
            UnitNumber = unit.UnitNumber,
            SectionNumber = unit.SectionNumber,
            HousingType = unit.HousingType,
            AreaSquareMeters = unit.AreaSquareMeters,
            OwnerName = unit.OwnerName,
            OwnerPhoneNumber = unit.OwnerPhoneNumber,
            OwnerEmail = unit.OwnerEmail,
            SecondOwnerName = unit.SecondOwnerName,
            SecondOwnerPhoneNumber = unit.SecondOwnerPhoneNumber,
            SecondOwnerEmail = unit.SecondOwnerEmail,
            Notes = unit.Notes
        };
    }

    public void ClearSecondOwner()
    {
        SecondOwnerName = null;
        SecondOwnerPhoneNumber = null;
        SecondOwnerEmail = null;
    }

    public HousingUnitDetails ToDetails()
    {
        return new HousingUnitDetails
        {
            Address = Address,
            UnitNumber = UnitNumber,
            SectionNumber = SectionNumber,
            HousingType = HousingType,
            AreaSquareMeters = AreaSquareMeters,
            OwnerName = OwnerName,
            OwnerPhoneNumber = OwnerPhoneNumber,
            OwnerEmail = OwnerEmail,
            SecondOwnerName = SecondOwnerName,
            SecondOwnerPhoneNumber = SecondOwnerPhoneNumber,
            SecondOwnerEmail = SecondOwnerEmail,
            Notes = Notes
        };
    }
}
