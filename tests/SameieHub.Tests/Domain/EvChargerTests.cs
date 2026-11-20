using SameieHub.Domain;

namespace SameieHub.Tests.Domain;

public class EvChargerTests
{
    private static readonly DateTime Created = new(2026, 11, 18, 10, 0, 0, DateTimeKind.Utc);

    private static EvCharger NewCharger(EvChargerDetails? details = null)
    {
        return new EvCharger(1, 5, details ?? new EvChargerDetails(), Created);
    }

    [Fact]
    public void Create_Defaults_AreInUseWithUnknownVehicleAndNoOptionalValues()
    {
        var charger = NewCharger();

        Assert.Equal(1, charger.ChargingFacilityId);
        Assert.Equal(5, charger.HousingUnitId);
        Assert.Equal(ChargerStatus.InUse, charger.Status);
        Assert.Null(charger.HasElectricVehicle);
        Assert.Null(charger.ChargerNumber);
        Assert.Null(charger.Manufacturer);
        Assert.Null(charger.Model);
        Assert.Null(charger.Notes);
        Assert.Equal(Created, charger.CreatedAt);
        Assert.Equal(Created, charger.UpdatedAt);
    }

    [Fact]
    public void Create_AllValues_AreTrimmedAndStored()
    {
        var charger = NewCharger(new EvChargerDetails
        {
            ChargerNumber = "  L3 ",
            Manufacturer = " Demo Charge ",
            Model = " EL-7 ",
            Status = ChargerStatus.NotInUse,
            HasElectricVehicle = false,
            Notes = " Fiktiv "
        });

        Assert.Equal("L3", charger.ChargerNumber);
        Assert.Equal("Demo Charge", charger.Manufacturer);
        Assert.Equal("EL-7", charger.Model);
        Assert.Equal(ChargerStatus.NotInUse, charger.Status);
        Assert.False(charger.HasElectricVehicle);
        Assert.Equal("Fiktiv", charger.Notes);
    }

    [Fact]
    public void Create_BlankOptionalValues_BecomeNull()
    {
        var charger = NewCharger(new EvChargerDetails { ChargerNumber = "  ", Manufacturer = "", Model = " ", Notes = "   " });

        Assert.Null(charger.ChargerNumber);
        Assert.Null(charger.Manufacturer);
        Assert.Null(charger.Model);
        Assert.Null(charger.Notes);
    }

    [Theory]
    [InlineData("ChargerNumber")]
    [InlineData("Manufacturer")]
    [InlineData("Model")]
    [InlineData("Notes")]
    public void Create_TooLongValue_IsRejectedWithThePropertyName(string property)
    {
        var tooLong = new string('A', 2001);
        var details = property switch
        {
            "ChargerNumber" => new EvChargerDetails { ChargerNumber = tooLong },
            "Manufacturer" => new EvChargerDetails { Manufacturer = tooLong },
            "Model" => new EvChargerDetails { Model = tooLong },
            _ => new EvChargerDetails { Notes = tooLong }
        };

        var exception = Assert.Throws<ArgumentException>(() => NewCharger(details));

        Assert.Equal(property, exception.ParamName);
    }

    [Fact]
    public void Create_InvalidStatus_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NewCharger(new EvChargerDetails { Status = (ChargerStatus)99 }));
    }

    [Fact]
    public void Create_NonUtcTimeOrInvalidIds_AreRejected()
    {
        Assert.Throws<ArgumentException>(() => new EvCharger(1, 5, new EvChargerDetails(), DateTime.SpecifyKind(Created, DateTimeKind.Unspecified)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EvCharger(0, 5, new EvChargerDetails(), Created));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EvCharger(1, 0, new EvChargerDetails(), Created));
    }

    [Fact]
    public void AssignId_OnlyOnceAndOnlyPositive()
    {
        var charger = NewCharger();

        Assert.Throws<ArgumentOutOfRangeException>(() => charger.AssignId(0));
        charger.AssignId(3);
        Assert.Equal(3, charger.Id);
        Assert.Throws<InvalidOperationException>(() => charger.AssignId(4));
    }

    // Changes

    [Fact]
    public void ChangeStatus_ChangesStatusAndUpdatedAt()
    {
        var charger = NewCharger();
        var later = Created.AddHours(2);

        charger.ChangeStatus(ChargerStatus.TemporarilyDisabled, later);

        Assert.Equal(ChargerStatus.TemporarilyDisabled, charger.Status);
        Assert.Equal(later, charger.UpdatedAt);
        Assert.Equal(Created, charger.CreatedAt);
    }

    [Fact]
    public void ChangeStatus_SameStatus_KeepsUpdatedAt()
    {
        var charger = NewCharger();

        charger.ChangeStatus(ChargerStatus.InUse, Created.AddHours(2));

        Assert.Equal(Created, charger.UpdatedAt);
    }

    [Fact]
    public void UpdateDetails_NothingChanged_KeepsUpdatedAt()
    {
        var charger = NewCharger(new EvChargerDetails { Model = "EL-7" });

        charger.UpdateDetails(charger.ToDetails(), Created.AddHours(2));

        Assert.Equal(Created, charger.UpdatedAt);
    }

    [Fact]
    public void UpdateDetails_InvalidValue_ChangesNothing()
    {
        var charger = NewCharger(new EvChargerDetails { Model = "EL-7", Status = ChargerStatus.NotInUse });

        Assert.Throws<ArgumentException>(() =>
            charger.UpdateDetails(new EvChargerDetails { Model = "Ny", Status = ChargerStatus.InUse, Notes = new string('A', 2001) }, Created.AddHours(2)));

        Assert.Equal("EL-7", charger.Model);
        Assert.Equal(ChargerStatus.NotInUse, charger.Status);
        Assert.Equal(Created, charger.UpdatedAt);
    }

    [Fact]
    public void Status_IsIndependentOfTheHousingUnit_ThereIsNoLinkToItsActiveFlag()
    {
        var charger = NewCharger(new EvChargerDetails { Status = ChargerStatus.TemporarilyDisabled });

        Assert.Equal(ChargerStatus.TemporarilyDisabled, charger.Status);
        Assert.Equal(5, charger.HousingUnitId);
    }
}

public class MeterAndFacilityTests
{
    private static readonly DateTime Created = new(2026, 11, 18, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Facility_NameIsRequiredAndTrimmed()
    {
        Assert.Equal("Ladeanlegg", new ChargingFacility("  Ladeanlegg ", null, Created).Name);
        Assert.Equal("Name", Assert.Throws<ArgumentException>(() => new ChargingFacility("  ", null, Created)).ParamName);
        Assert.Equal("Name", Assert.Throws<ArgumentException>(() => new ChargingFacility(new string('A', 101), null, Created)).ParamName);
    }

    [Fact]
    public void MainMeter_BelongsToTheFacilityAndHasNoCharger()
    {
        var meter = Meter.CreateMain(1, "Hovedmåler", Created);

        Assert.Equal(MeterKind.Main, meter.Kind);
        Assert.True(meter.IsMain);
        Assert.Null(meter.EvChargerId);
        Assert.Equal(1, meter.ChargingFacilityId);
    }

    [Fact]
    public void SubMeter_BelongsToOneCharger()
    {
        var meter = Meter.CreateSub(1, 4, "Undermåler L4", 2, Created);

        Assert.Equal(MeterKind.Sub, meter.Kind);
        Assert.False(meter.IsMain);
        Assert.Equal(4, meter.EvChargerId);
    }

    [Fact]
    public void Meter_InvalidValues_AreRejected()
    {
        Assert.Throws<ArgumentException>(() => Meter.CreateMain(1, " ", Created));
        Assert.Throws<ArgumentOutOfRangeException>(() => Meter.CreateSub(1, 0, "Undermåler", 1, Created));
        Assert.Throws<ArgumentOutOfRangeException>(() => Meter.CreateMain(0, "Hovedmåler", Created));
        Assert.Throws<ArgumentException>(() => Meter.CreateMain(1, "Hovedmåler", DateTime.SpecifyKind(Created, DateTimeKind.Local)));
    }

    [Fact]
    public void Meter_AssignId_OnlyOnce()
    {
        var meter = Meter.CreateMain(1, "Hovedmåler", Created);

        meter.AssignId(2);

        Assert.Equal(2, meter.Id);
        Assert.Throws<InvalidOperationException>(() => meter.AssignId(3));
    }
}
