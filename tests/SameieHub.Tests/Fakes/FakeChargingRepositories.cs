using SameieHub.Domain;

namespace SameieHub.Tests.Fakes;

public class FakeChargingFacilityRepository : IChargingFacilityRepository
{
    private ChargingFacility? _facility;

    public Task AddAsync(ChargingFacility facility)
    {
        facility.AssignId(1);
        _facility = facility;

        return Task.CompletedTask;
    }

    public Task<ChargingFacility?> GetAsync()
    {
        return Task.FromResult(_facility);
    }
}

public class FakeEvChargerRepository : IEvChargerRepository
{
    private readonly Dictionary<int, EvCharger> _chargers = [];
    private int _nextId = 1;

    public Task AddAsync(EvCharger charger)
    {
        if (_chargers.Values.Any(existing => existing.HousingUnitId == charger.HousingUnitId))
        {
            throw new EvChargerAlreadyExistsException(charger.HousingUnitId);
        }

        charger.AssignId(_nextId++);
        _chargers[charger.Id] = charger;

        return Task.CompletedTask;
    }

    public Task<EvCharger?> GetByIdAsync(int id)
    {
        _chargers.TryGetValue(id, out var charger);

        return Task.FromResult(charger);
    }

    public Task<EvCharger?> GetByHousingUnitIdAsync(int housingUnitId)
    {
        return Task.FromResult(_chargers.Values.FirstOrDefault(charger => charger.HousingUnitId == housingUnitId));
    }

    public Task<IReadOnlyList<EvCharger>> GetAllAsync()
    {
        IReadOnlyList<EvCharger> chargers = _chargers.Values.OrderBy(charger => charger.Id).ToList();

        return Task.FromResult(chargers);
    }

    public Task UpdateAsync(EvCharger charger)
    {
        return Task.CompletedTask;
    }
}

public class FakeMeterRepository : IMeterRepository
{
    private readonly Dictionary<int, Meter> _meters = [];
    private int _nextId = 1;

    public Task AddAsync(Meter meter)
    {
        meter.AssignId(_nextId++);
        _meters[meter.Id] = meter;

        return Task.CompletedTask;
    }

    public Task<Meter?> GetByIdAsync(int id)
    {
        _meters.TryGetValue(id, out var meter);

        return Task.FromResult(meter);
    }

    public Task<IReadOnlyList<Meter>> GetAllAsync()
    {
        IReadOnlyList<Meter> meters = _meters.Values.OrderBy(meter => meter.Id).ToList();

        return Task.FromResult(meters);
    }

    public Task UpdateAsync(Meter meter)
    {
        return Task.CompletedTask;
    }
}

public class FakeMeterReadingRepository : IMeterReadingRepository
{
    private readonly Dictionary<int, MeterReading> _readings = [];
    private int _nextId = 1;

    public Task AddAsync(MeterReading reading)
    {
        reading.AssignId(_nextId++);
        _readings[reading.Id] = reading;

        return Task.CompletedTask;
    }

    public Task<MeterReading?> GetByIdAsync(int id)
    {
        _readings.TryGetValue(id, out var reading);

        return Task.FromResult(reading);
    }

    public Task<IReadOnlyList<MeterReading>> GetByMeterIdAsync(int meterId)
    {
        IReadOnlyList<MeterReading> readings = _readings.Values.Where(reading => reading.MeterId == meterId).ToList();

        return Task.FromResult(readings);
    }

    public Task<IReadOnlyList<MeterReading>> GetAllAsync()
    {
        IReadOnlyList<MeterReading> readings = _readings.Values.ToList();

        return Task.FromResult(readings);
    }

    public Task UpdateAsync(MeterReading reading)
    {
        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(int id)
    {
        return Task.FromResult(_readings.Remove(id));
    }
}
