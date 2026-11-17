namespace SameieHub.Domain;

public class HousingUnit
{
    public int Id { get; private set; }

    public string Address { get; private set; }

    public HousingUnit(string address)
    {
        Address = NormalizeAddress(address);
    }

    /// <summary>
    /// Called by a repository when the unit is stored. The Id can only be assigned once.
    /// </summary>
    public void AssignId(int id)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);

        if (Id != 0)
        {
            throw new InvalidOperationException($"Housing unit already has Id {Id}.");
        }

        Id = id;
    }

    public void ChangeAddress(string newAddress)
    {
        Address = NormalizeAddress(newAddress);
    }

    private static string NormalizeAddress(string? address)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);

        return address.Trim();
    }
}
