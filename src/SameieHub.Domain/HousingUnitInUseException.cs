namespace SameieHub.Domain;

/// <summary>
/// A housing unit cannot be deleted because other records, such as board cases or tasks, refer to it.
/// </summary>
public class HousingUnitInUseException : Exception
{
    public HousingUnitInUseException(int housingUnitId, Exception? innerException = null)
        : base($"Housing unit {housingUnitId} is used by other records and cannot be deleted.", innerException)
    {
    }
}
