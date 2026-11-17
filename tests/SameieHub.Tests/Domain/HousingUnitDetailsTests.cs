using SameieHub.Domain;

namespace SameieHub.Tests.Domain;

public class HousingUnitDetailsTests
{
    private static HousingUnitDetails ValidDetails() => new()
    {
        Address = "Storgata 1A",
        UnitNumber = "H0101",
        SectionNumber = 12,
        HousingType = HousingType.Duplex,
        AreaSquareMeters = 85.5m,
        OwnerName = "Test Eier",
        OwnerPhoneNumber = "+47 400 00 000",
        OwnerEmail = "eier@example.test",
        SecondOwnerName = "Test Medeier",
        SecondOwnerPhoneNumber = "400 00 001",
        SecondOwnerEmail = "medeier@example.test",
        Notes = "Fiktive testdata."
    };

    [Fact]
    public void Constructor_OnlyAddress_IsActiveWithNoOptionalValues()
    {
        var unit = new HousingUnit("Storgata 1A");

        Assert.True(unit.IsActive);
        Assert.Null(unit.UnitNumber);
        Assert.Null(unit.SectionNumber);
        Assert.Null(unit.HousingType);
        Assert.Null(unit.AreaSquareMeters);
        Assert.Null(unit.OwnerName);
        Assert.Null(unit.SecondOwnerName);
        Assert.Null(unit.Notes);
    }

    [Fact]
    public void Constructor_AllDetails_SetsAllValues()
    {
        var unit = new HousingUnit(ValidDetails());

        AssertHasValues(unit, ValidDetails());
        Assert.True(unit.IsActive);
    }

    [Fact]
    public void Constructor_InvalidDetails_Throws()
    {
        var invalid = ValidDetails() with { OwnerEmail = "not-an-email" };

        Assert.Throws<ArgumentException>(() => new HousingUnit(invalid));
    }

    [Fact]
    public void UpdateDetails_NewValues_ReplacesEverything()
    {
        var unit = new HousingUnit(ValidDetails());
        var changed = new HousingUnitDetails
        {
            Address = "Storgata 2B",
            UnitNumber = "H0202",
            SectionNumber = 13,
            HousingType = HousingType.Apartment,
            AreaSquareMeters = 60m,
            OwnerName = "Annen Eier",
            Notes = "Endret"
        };

        unit.UpdateDetails(changed);

        AssertHasValues(unit, changed);
        Assert.Null(unit.OwnerPhoneNumber);
        Assert.Null(unit.SecondOwnerName);
    }

    [Fact]
    public void UpdateDetails_TextWithSurroundingSpaces_IsTrimmed()
    {
        var unit = new HousingUnit("Storgata 1A");

        unit.UpdateDetails(new HousingUnitDetails
        {
            Address = "  Storgata 1A  ",
            UnitNumber = "  H0101 ",
            OwnerName = " Test Eier ",
            OwnerEmail = " eier@example.test ",
            OwnerPhoneNumber = " 400 00 000 ",
            Notes = "  Merknad  "
        });

        Assert.Equal("Storgata 1A", unit.Address);
        Assert.Equal("H0101", unit.UnitNumber);
        Assert.Equal("Test Eier", unit.OwnerName);
        Assert.Equal("eier@example.test", unit.OwnerEmail);
        Assert.Equal("400 00 000", unit.OwnerPhoneNumber);
        Assert.Equal("Merknad", unit.Notes);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void UpdateDetails_EmptyOptionalText_BecomesNull(string? empty)
    {
        var unit = new HousingUnit(ValidDetails());

        unit.UpdateDetails(ValidDetails() with
        {
            UnitNumber = empty,
            OwnerName = empty,
            OwnerPhoneNumber = empty,
            OwnerEmail = empty,
            SecondOwnerName = empty,
            SecondOwnerPhoneNumber = empty,
            SecondOwnerEmail = empty,
            Notes = empty
        });

        Assert.Null(unit.UnitNumber);
        Assert.Null(unit.OwnerName);
        Assert.Null(unit.OwnerPhoneNumber);
        Assert.Null(unit.OwnerEmail);
        Assert.Null(unit.SecondOwnerName);
        Assert.Null(unit.SecondOwnerPhoneNumber);
        Assert.Null(unit.SecondOwnerEmail);
        Assert.Null(unit.Notes);
    }

    [Fact]
    public void UpdateDetails_RemovingSecondOwner_KeepsFirstOwnerUnchanged()
    {
        var unit = new HousingUnit(ValidDetails());

        unit.UpdateDetails(ValidDetails() with
        {
            SecondOwnerName = null,
            SecondOwnerPhoneNumber = null,
            SecondOwnerEmail = null
        });

        Assert.Equal("Test Eier", unit.OwnerName);
        Assert.Equal("+47 400 00 000", unit.OwnerPhoneNumber);
        Assert.Equal("eier@example.test", unit.OwnerEmail);
        Assert.Null(unit.SecondOwnerName);
        Assert.Null(unit.SecondOwnerPhoneNumber);
        Assert.Null(unit.SecondOwnerEmail);
    }

    [Fact]
    public void UpdateDetails_ChangingSecondOwner_KeepsFirstOwnerUnchanged()
    {
        var unit = new HousingUnit(ValidDetails());

        unit.UpdateDetails(ValidDetails() with { SecondOwnerName = "Ny Medeier" });

        Assert.Equal("Ny Medeier", unit.SecondOwnerName);
        Assert.Equal("Test Eier", unit.OwnerName);
    }

    [Fact]
    public void UpdateDetails_ValuesAtTheLimits_AreAccepted()
    {
        var unit = new HousingUnit("Storgata 1A");

        unit.UpdateDetails(new HousingUnitDetails
        {
            Address = new string('a', HousingUnit.MaxAddressLength),
            UnitNumber = new string('u', HousingUnit.MaxUnitNumberLength),
            SectionNumber = HousingUnit.MaxSectionNumber,
            AreaSquareMeters = HousingUnit.MaxAreaSquareMeters,
            OwnerName = new string('n', HousingUnit.MaxOwnerNameLength),
            OwnerEmail = new string('e', HousingUnit.MaxEmailLength - "@example.test".Length) + "@example.test",
            OwnerPhoneNumber = new string('1', HousingUnit.MaxPhoneNumberLength),
            Notes = new string('m', HousingUnit.MaxNotesLength)
        });

        Assert.Equal(HousingUnit.MaxSectionNumber, unit.SectionNumber);
    }

    [Fact]
    public void UpdateDetails_BlankAddress_ThrowsAndChangesNothing()
    {
        AssertRejectedAndUnchanged(ValidDetails() with { Address = "   ", OwnerName = "Skal ikke lagres" }, "Address");
    }

    [Theory]
    [InlineData("no-at-sign")]
    [InlineData("a@")]
    [InlineData("@example.test")]
    [InlineData("a b@example.test")]
    [InlineData("Navn <a@example.test>")]
    public void UpdateDetails_InvalidEmail_ThrowsAndChangesNothing(string email)
    {
        AssertRejectedAndUnchanged(ValidDetails() with { Address = "Annen adresse", OwnerEmail = email }, "OwnerEmail");
        AssertRejectedAndUnchanged(ValidDetails() with { Address = "Annen adresse", SecondOwnerEmail = email }, "SecondOwnerEmail");
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("12")]
    [InlineData("400 00 000 ext 5")]
    public void UpdateDetails_InvalidPhoneNumber_ThrowsAndChangesNothing(string phone)
    {
        AssertRejectedAndUnchanged(ValidDetails() with { Address = "Annen adresse", OwnerPhoneNumber = phone }, "OwnerPhoneNumber");
        AssertRejectedAndUnchanged(ValidDetails() with { Address = "Annen adresse", SecondOwnerPhoneNumber = phone }, "SecondOwnerPhoneNumber");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(HousingUnit.MaxSectionNumber + 1)]
    public void UpdateDetails_SectionNumberOutOfRange_ThrowsAndChangesNothing(int section)
    {
        AssertRejectedAndUnchanged(ValidDetails() with { Address = "Annen adresse", SectionNumber = section }, "SectionNumber");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(HousingUnit.MaxAreaSquareMeters + 1)]
    public void UpdateDetails_AreaOutOfRange_ThrowsAndChangesNothing(int area)
    {
        AssertRejectedAndUnchanged(ValidDetails() with { Address = "Annen adresse", AreaSquareMeters = area }, "AreaSquareMeters");
    }

    [Fact]
    public void UpdateDetails_TooLongText_ThrowsAndChangesNothing()
    {
        var tooLongName = new string('n', HousingUnit.MaxOwnerNameLength + 1);

        AssertRejectedAndUnchanged(ValidDetails() with { Address = new string('a', HousingUnit.MaxAddressLength + 1) }, "Address");
        AssertRejectedAndUnchanged(ValidDetails() with { UnitNumber = new string('u', HousingUnit.MaxUnitNumberLength + 1) }, "UnitNumber");
        AssertRejectedAndUnchanged(ValidDetails() with { OwnerName = tooLongName }, "OwnerName");
        AssertRejectedAndUnchanged(ValidDetails() with { SecondOwnerName = tooLongName }, "SecondOwnerName");
        AssertRejectedAndUnchanged(ValidDetails() with { Notes = new string('m', HousingUnit.MaxNotesLength + 1) }, "Notes");
    }

    [Fact]
    public void UpdateDetails_InvalidValue_ErrorMessageDoesNotContainTheValue()
    {
        var unit = new HousingUnit("Storgata 1A");

        var exception = Assert.Throws<ArgumentException>(() =>
            unit.UpdateDetails(new HousingUnitDetails { Address = "Storgata 1A", OwnerEmail = "hemmelig-verdi" }));

        Assert.DoesNotContain("hemmelig-verdi", exception.Message);
    }

    [Fact]
    public void Deactivate_ActiveUnit_BecomesInactiveAndKeepsData()
    {
        var unit = new HousingUnit(ValidDetails());

        unit.Deactivate();

        Assert.False(unit.IsActive);
        AssertHasValues(unit, ValidDetails());
    }

    [Fact]
    public void Deactivate_AlreadyInactive_StaysInactive()
    {
        var unit = new HousingUnit("Storgata 1A");
        unit.Deactivate();

        unit.Deactivate();

        Assert.False(unit.IsActive);
    }

    [Fact]
    public void Activate_InactiveUnit_BecomesActive()
    {
        var unit = new HousingUnit("Storgata 1A");
        unit.Deactivate();

        unit.Activate();

        Assert.True(unit.IsActive);
    }

    [Fact]
    public void UpdateDetails_InactiveUnit_StaysInactive()
    {
        var unit = new HousingUnit("Storgata 1A");
        unit.Deactivate();

        unit.UpdateDetails(ValidDetails());

        Assert.False(unit.IsActive);
    }

    private static void AssertRejectedAndUnchanged(HousingUnitDetails invalid, string expectedField)
    {
        var unit = new HousingUnit(ValidDetails());

        var exception = Assert.ThrowsAny<ArgumentException>(() => unit.UpdateDetails(invalid));

        Assert.Equal(expectedField, exception.ParamName);
        AssertHasValues(unit, ValidDetails());
    }

    private static void AssertHasValues(HousingUnit unit, HousingUnitDetails expected)
    {
        Assert.Equal(expected.Address, unit.Address);
        Assert.Equal(expected.UnitNumber, unit.UnitNumber);
        Assert.Equal(expected.SectionNumber, unit.SectionNumber);
        Assert.Equal(expected.HousingType, unit.HousingType);
        Assert.Equal(expected.AreaSquareMeters, unit.AreaSquareMeters);
        Assert.Equal(expected.OwnerName, unit.OwnerName);
        Assert.Equal(expected.OwnerPhoneNumber, unit.OwnerPhoneNumber);
        Assert.Equal(expected.OwnerEmail, unit.OwnerEmail);
        Assert.Equal(expected.SecondOwnerName, unit.SecondOwnerName);
        Assert.Equal(expected.SecondOwnerPhoneNumber, unit.SecondOwnerPhoneNumber);
        Assert.Equal(expected.SecondOwnerEmail, unit.SecondOwnerEmail);
        Assert.Equal(expected.Notes, unit.Notes);
    }
}
