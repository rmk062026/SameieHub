using System.ComponentModel.DataAnnotations;

namespace SameieHub.Web.Components.Pages.HousingUnits;

public class HousingUnitFormModel
{
    [Required(ErrorMessage = "Adressen må fylles ut.")]
    public string Address { get; set; } = string.Empty;
}
