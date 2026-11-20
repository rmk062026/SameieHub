namespace SameieHub.Domain;

public interface IChargingFacilityRepository
{
    /// <summary>Stores the facility. When the task completes, <see cref="ChargingFacility.Id"/> has been assigned.</summary>
    Task AddAsync(ChargingFacility facility);

    /// <summary>The one charging facility, or null when none is set up.</summary>
    Task<ChargingFacility?> GetAsync();
}

public interface IEvChargerRepository
{
    /// <summary>Stores a new charger. When the task completes, <see cref="EvCharger.Id"/> has been assigned.</summary>
    /// <exception cref="EvChargerAlreadyExistsException">The housing unit already has a charger. Nothing is stored.</exception>
    Task AddAsync(EvCharger charger);

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
    Task AddAsync(MeterReading reading);

    Task<MeterReading?> GetByIdAsync(int id);

    /// <summary>The readings of one meter, in no particular order.</summary>
    Task<IReadOnlyList<MeterReading>> GetByMeterIdAsync(int meterId);

    Task<IReadOnlyList<MeterReading>> GetAllAsync();

    Task UpdateAsync(MeterReading reading);

    /// <summary>Returns false when no reading has the given Id.</summary>
    Task<bool> DeleteAsync(int id);
}
