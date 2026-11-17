namespace SameieHub.Domain;

public class HousingUnit
{
    public const int MaxAddressLength = 200;
    public const int MaxUnitNumberLength = 20;
    public const int MinSectionNumber = 1;
    public const int MaxSectionNumber = 9999;
    public const int MaxAreaSquareMeters = 1000;
    public const int MaxOwnerNameLength = 100;
    public const int MinPhoneDigits = 5;
    public const int MaxPhoneNumberLength = 30;
    public const int MaxEmailLength = 254;
    public const int MaxNotesLength = 2000;

    public int Id { get; private set; }

    public string Address { get; private set; }

    public string? UnitNumber { get; private set; }

    public int? SectionNumber { get; private set; }

    public HousingType? HousingType { get; private set; }

    public decimal? AreaSquareMeters { get; private set; }

    public string? OwnerName { get; private set; }

    public string? OwnerPhoneNumber { get; private set; }

    public string? OwnerEmail { get; private set; }

    public string? SecondOwnerName { get; private set; }

    public string? SecondOwnerPhoneNumber { get; private set; }

    public string? SecondOwnerEmail { get; private set; }

    public string? Notes { get; private set; }

    public bool IsActive { get; private set; } = true;

    public HousingUnit(string address)
    {
        Address = NormalizeAddress(address);
    }

    public HousingUnit(HousingUnitDetails details)
        : this(details.Address)
    {
        UpdateDetails(details);
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

    /// <summary>
    /// Replaces all details. Either every value is valid and applied, or nothing changes.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// A value is invalid. <see cref="ArgumentException.ParamName"/> is the name of the property in <see cref="HousingUnitDetails"/>.
    /// </exception>
    public void UpdateDetails(HousingUnitDetails details)
    {
        // Validate everything first so an invalid value cannot leave the unit half updated.
        var address = NormalizeAddress(details.Address, nameof(details.Address));
        var unitNumber = NormalizeOptionalText(details.UnitNumber, MaxUnitNumberLength, nameof(details.UnitNumber));
        var sectionNumber = ValidateSectionNumber(details.SectionNumber);
        var areaSquareMeters = ValidateArea(details.AreaSquareMeters);
        var ownerName = NormalizeOptionalText(details.OwnerName, MaxOwnerNameLength, nameof(details.OwnerName));
        var ownerPhoneNumber = NormalizePhoneNumber(details.OwnerPhoneNumber, nameof(details.OwnerPhoneNumber));
        var ownerEmail = NormalizeEmail(details.OwnerEmail, nameof(details.OwnerEmail));
        var secondOwnerName = NormalizeOptionalText(details.SecondOwnerName, MaxOwnerNameLength, nameof(details.SecondOwnerName));
        var secondOwnerPhoneNumber = NormalizePhoneNumber(details.SecondOwnerPhoneNumber, nameof(details.SecondOwnerPhoneNumber));
        var secondOwnerEmail = NormalizeEmail(details.SecondOwnerEmail, nameof(details.SecondOwnerEmail));
        var notes = NormalizeOptionalText(details.Notes, MaxNotesLength, nameof(details.Notes));

        Address = address;
        UnitNumber = unitNumber;
        SectionNumber = sectionNumber;
        HousingType = details.HousingType;
        AreaSquareMeters = areaSquareMeters;
        OwnerName = ownerName;
        OwnerPhoneNumber = ownerPhoneNumber;
        OwnerEmail = ownerEmail;
        SecondOwnerName = secondOwnerName;
        SecondOwnerPhoneNumber = secondOwnerPhoneNumber;
        SecondOwnerEmail = secondOwnerEmail;
        Notes = notes;
    }

    /// <summary>
    /// Deactivating keeps all data. Calling it on an already inactive unit changes nothing.
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
    }

    public void Activate()
    {
        IsActive = true;
    }

    private static string NormalizeAddress(string? address, string fieldName = nameof(Address))
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentException("Address is required.", fieldName);
        }

        var trimmed = address.Trim();
        if (trimmed.Length > MaxAddressLength)
        {
            throw new ArgumentException($"Address can be at most {MaxAddressLength} characters.", fieldName);
        }

        return trimmed;
    }

    private static string? NormalizeOptionalText(string? value, int maxLength, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"{fieldName} can be at most {maxLength} characters.", fieldName);
        }

        return trimmed;
    }

    private static string? NormalizePhoneNumber(string? value, string fieldName)
    {
        var phoneNumber = NormalizeOptionalText(value, MaxPhoneNumberLength, fieldName);
        if (phoneNumber == null)
        {
            return null;
        }

        var digitCount = 0;
        foreach (var character in phoneNumber)
        {
            if (character >= '0' && character <= '9')
            {
                digitCount++;
            }
            else if ("+-() ".IndexOf(character) < 0)
            {
                throw new ArgumentException($"{fieldName} contains invalid characters.", fieldName);
            }
        }

        if (digitCount < MinPhoneDigits)
        {
            throw new ArgumentException($"{fieldName} must contain at least {MinPhoneDigits} digits.", fieldName);
        }

        return phoneNumber;
    }

    private static string? NormalizeEmail(string? value, string fieldName)
    {
        var email = NormalizeOptionalText(value, MaxEmailLength, fieldName);
        if (email == null)
        {
            return null;
        }

        // MailAddress also accepts "Name <a@b.c>", so the parsed address must equal the input.
        var isValid = System.Net.Mail.MailAddress.TryCreate(email, out var parsed) && parsed.Address == email;
        if (!isValid)
        {
            throw new ArgumentException($"{fieldName} is not a valid email address.", fieldName);
        }

        return email;
    }

    private static int? ValidateSectionNumber(int? sectionNumber)
    {
        if (sectionNumber < MinSectionNumber || sectionNumber > MaxSectionNumber)
        {
            throw new ArgumentOutOfRangeException(
                nameof(HousingUnitDetails.SectionNumber), $"SectionNumber must be between {MinSectionNumber} and {MaxSectionNumber}.");
        }

        return sectionNumber;
    }

    private static decimal? ValidateArea(decimal? areaSquareMeters)
    {
        if (areaSquareMeters <= 0 || areaSquareMeters > MaxAreaSquareMeters)
        {
            throw new ArgumentOutOfRangeException(
                nameof(HousingUnitDetails.AreaSquareMeters), $"AreaSquareMeters must be greater than 0 and at most {MaxAreaSquareMeters}.");
        }

        return areaSquareMeters;
    }
}
