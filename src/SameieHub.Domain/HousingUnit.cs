namespace SameieHub.Domain;

public class HousingUnit
{
    public int Id { get; private set; }

    public string Address { get; private set; }

    public HousingUnit(string address)
    {
        Address = NormalizeAddress(address);
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
