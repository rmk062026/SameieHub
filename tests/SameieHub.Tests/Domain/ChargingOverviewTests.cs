using SameieHub.Domain;

namespace SameieHub.Tests.Domain;

public class ChargingOverviewTests
{
    private static readonly DateTime Created = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly List<HousingUnit> _units = [];
    private readonly List<EvCharger> _chargers = [];
    private readonly List<Meter> _meters = [];
    private readonly List<MeterReading> _readings = [];

    private HousingUnit AddUnit(string address)
    {
        var unit = new HousingUnit(address);
        unit.AssignId(_units.Count + 1);
        _units.Add(unit);

        return unit;
    }

    private Meter AddMain()
    {
        var meter = Meter.CreateMain(1, "Hovedmåler", Created);
        meter.AssignId(_meters.Count + 1);
        _meters.Add(meter);

        return meter;
    }

    private (EvCharger Charger, Meter Meter) AddCharger(HousingUnit unit, ChargerStatus status = ChargerStatus.InUse)
    {
        var charger = new EvCharger(1, unit.Id, new EvChargerDetails { Status = status }, Created);
        charger.AssignId(_chargers.Count + 1);
        _chargers.Add(charger);
        var meter = Meter.CreateSub(1, charger.Id, $"Undermåler {unit.Address}", 1, Created);
        meter.AssignId(_meters.Count + 1);
        _meters.Add(meter);

        return (charger, meter);
    }

    private void Read(Meter meter, int daysAgo, decimal kwh, int registeredDaysAgo = 0)
    {
        var readAt = new DateTime(2026, 11, 18, 8, 0, 0, DateTimeKind.Utc).AddDays(-daysAgo);
        var reading = new MeterReading(meter.Id, new MeterReadingDetails { ReadAt = readAt, Kwh = kwh }, Created.AddDays(registeredDaysAgo));
        reading.AssignId(_readings.Count + 1);
        _readings.Add(reading);
    }

    private ChargingOverview Create() => ChargingOverview.Create(_units, _chargers, _meters, _readings);

    [Fact]
    public void Create_NothingSetUp_HasZeroCountsAndAllUnitsWithoutCharger()
    {
        AddUnit("Fiktivdalen 1A");
        AddUnit("Fiktivdalen 1B");

        var overview = Create();

        Assert.Equal(0, overview.RegisteredChargers);
        Assert.Equal(0, overview.InUse);
        Assert.Equal(0, overview.NotInUse);
        Assert.Equal(0, overview.TemporarilyDisabled);
        Assert.Empty(overview.Rows);
        Assert.Equal(2, overview.UnitsWithoutCharger.Count);
        Assert.Null(overview.MainMeter);
        Assert.Null(overview.MainMeterLastReading);
    }

    [Fact]
    public void Create_CountsChargersPerAdministrativeStatus()
    {
        AddCharger(AddUnit("A"), ChargerStatus.InUse);
        AddCharger(AddUnit("B"), ChargerStatus.InUse);
        AddCharger(AddUnit("C"), ChargerStatus.NotInUse);
        AddCharger(AddUnit("D"), ChargerStatus.TemporarilyDisabled);
        AddCharger(AddUnit("E"), ChargerStatus.TemporarilyDisabled);

        var overview = Create();

        Assert.Equal(5, overview.RegisteredChargers);
        Assert.Equal(2, overview.InUse);
        Assert.Equal(1, overview.NotInUse);
        Assert.Equal(2, overview.TemporarilyDisabled);
        Assert.Empty(overview.UnitsWithoutCharger);
    }

    [Fact]
    public void Create_ChargerStatusIsIndependentOfWhetherTheHousingUnitIsActive()
    {
        var unit = AddUnit("A");
        unit.Deactivate();
        AddCharger(unit, ChargerStatus.InUse);

        var overview = Create();

        Assert.Equal(1, overview.InUse);
        Assert.Empty(overview.UnitsWithoutCharger);
    }

    [Fact]
    public void Create_RowsAreSortedByAddressAndCarryUnitChargerSubMeterAndCabinet()
    {
        var second = AddUnit("Fiktivdalen 12A");
        var first = AddUnit("Fiktivdalen 10A");
        AddCharger(second);
        var (firstCharger, firstMeter) = AddCharger(first);

        var overview = Create();

        Assert.Equal(["Fiktivdalen 10A", "Fiktivdalen 12A"], overview.Rows.Select(row => row.Unit.Address));
        Assert.Equal(firstCharger.Id, overview.Rows[0].Charger.Id);
        Assert.Equal(firstMeter.Id, overview.Rows[0].SubMeter!.Id);
        Assert.Equal(1, overview.Rows[0].SubMeter!.CabinetNumber);
    }

    [Fact]
    public void Create_LastReadingIsTheLatestByReadTimeNotByRegistrationOrder()
    {
        var (_, meter) = AddCharger(AddUnit("A"));
        Read(meter, daysAgo: 5, kwh: 150m, registeredDaysAgo: 0);
        Read(meter, daysAgo: 40, kwh: 100m, registeredDaysAgo: 10);

        var overview = Create();

        Assert.Equal(150m, overview.Rows[0].LastReading!.Kwh);
    }

    [Fact]
    public void Create_ChargerWithoutReadingsHasNoLastReadingNotZero()
    {
        AddCharger(AddUnit("A"));

        Assert.Null(Create().Rows[0].LastReading);
    }

    [Fact]
    public void Create_MainMeterAndItsLastReading()
    {
        var main = AddMain();
        Read(main, 30, 5000m);
        Read(main, 2, 5800m);

        var overview = Create();

        Assert.Equal(main.Id, overview.MainMeter!.Id);
        Assert.Equal(5800m, overview.MainMeterLastReading!.Kwh);
    }

    [Fact]
    public void Create_UnitsWithoutChargerAreListedSeparately()
    {
        AddCharger(AddUnit("B"));
        AddUnit("A");

        var overview = Create();

        Assert.Equal(["A"], overview.UnitsWithoutCharger.Select(unit => unit.Address));
        Assert.Single(overview.Rows);
    }

    [Fact]
    public void Create_DeactivatedChargerKeepsItsLastReading()
    {
        var (_, meter) = AddCharger(AddUnit("A"), ChargerStatus.TemporarilyDisabled);
        Read(meter, 20, 321.5m);

        var overview = Create();

        Assert.Equal(321.5m, overview.Rows[0].LastReading!.Kwh);
        Assert.Equal(1, overview.TemporarilyDisabled);
    }
}
