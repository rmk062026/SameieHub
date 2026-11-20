namespace SameieHub.Domain;

public interface IChargingFacilityRepository
{
    /// <summary>
    /// Stores the facility together with its main meter, or neither. <paramref name="createMainMeter"/> receives the
    /// Id of the stored facility and returns the main meter. When the task completes, the Ids have been assigned.
    /// </summary>
    /// <exception cref="ChargingFacilityAlreadyExistsException">A facility already exists. Nothing is stored.</exception>
    Task AddWithMainMeterAsync(ChargingFacility facility, Func<int, Meter> createMainMeter);

    /// <summary>The one charging facility, or null when none is set up.</summary>
    Task<ChargingFacility?> GetAsync();
}

public interface IEvChargerRepository
{
    /// <summary>
    /// Stores the charger together with its sub meter, or neither, so a charger never exists without a meter.
    /// <paramref name="createSubMeter"/> receives the Id of the stored charger and returns its sub meter.
    /// When the task completes, the Ids have been assigned.
    /// </summary>
    /// <exception cref="EvChargerAlreadyExistsException">The housing unit already has a charger. Nothing is stored.</exception>
    Task AddWithSubMeterAsync(EvCharger charger, Func<int, Meter> createSubMeter);

    Task<EvCharger?> GetByIdAsync(int id);

    Task<EvCharger?> GetByHousingUnitIdAsync(int housingUnitId);

    Task<IReadOnlyList<EvCharger>> GetAllAsync();

    Task UpdateAsync(EvCharger charger);
}

public interface IMeterRepository
{
    /// <summary>Stores a new meter. When the task completes, <see cref="Meter.Id"/> has been assigned.</summary>
    Task AddAsync(Meter meter);

    Task<Meter?> GetByIdAsync(int id);

    Task<IReadOnlyList<Meter>> GetAllAsync();

    Task UpdateAsync(Meter meter);
}

public interface IMeterReadingRepository
{
    /// <summary>Stores a new reading. When the task completes, <see cref="MeterReading.Id"/> has been assigned.</summary>
    /// <exception cref="ArgumentException">The meter already has a reading at this time (ParamName "ReadAt").</exception>
    Task AddAsync(MeterReading reading);

    /// <summary>
    /// Stores all the readings, or none of them, in one database transaction. This is what makes a reading set
    /// for up to nine meters atomic.
    /// </summary>
    /// <exception cref="ArgumentException">A meter already has a reading at this time (ParamName "ReadAt"). Nothing is stored.</exception>
    Task AddRangeAsync(IReadOnlyList<MeterReading> readings);

    Task<MeterReading?> GetByIdAsync(int id);

    /// <summary>The readings of one meter, in no particular order.</summary>
    Task<IReadOnlyList<MeterReading>> GetByMeterIdAsync(int meterId);

    Task<IReadOnlyList<MeterReading>> GetAllAsync();

    /// <exception cref="ArgumentException">Another reading of the meter has the same time (ParamName "ReadAt").</exception>
    Task UpdateAsync(MeterReading reading);

    /// <summary>Returns false when no reading has the given Id.</summary>
    Task<bool> DeleteAsync(int id);
}
