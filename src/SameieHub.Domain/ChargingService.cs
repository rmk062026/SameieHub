namespace SameieHub.Domain;

/// <summary>
/// Sets up the charging facility, its chargers and meters, and registers and corrects meter readings.
/// All readings are entered by hand. Nothing here controls a charger or talks to an external system.
/// </summary>
public class ChargingService
{
    public const string MainMeterName = "Hovedmåler";

    private readonly IChargingFacilityRepository _facilityRepository;
    private readonly IEvChargerRepository _chargerRepository;
    private readonly IMeterRepository _meterRepository;
    private readonly IMeterReadingRepository _readingRepository;
    private readonly IHousingUnitRepository _housingUnitRepository;
    private readonly TimeProvider _timeProvider;

    public ChargingService(
        IChargingFacilityRepository facilityRepository,
        IEvChargerRepository chargerRepository,
        IMeterRepository meterRepository,
        IMeterReadingRepository readingRepository,
        IHousingUnitRepository housingUnitRepository,
        TimeProvider timeProvider)
    {
        _facilityRepository = facilityRepository;
        _chargerRepository = chargerRepository;
        _meterRepository = meterRepository;
        _readingRepository = readingRepository;
        _housingUnitRepository = housingUnitRepository;
        _timeProvider = timeProvider;
    }

    // Facility, chargers and meters

    /// <summary>Creates the one charging facility with its main meter.</summary>
    /// <exception cref="ChargingFacilityAlreadyExistsException">A facility is already set up.</exception>
    public async Task<ChargingFacility> SetUpFacilityAsync(string name)
    {
        if (await _facilityRepository.GetAsync() != null)
        {
            throw new ChargingFacilityAlreadyExistsException();
        }

        var facility = new ChargingFacility(name, null, GetUtcNow());
        await _facilityRepository.AddAsync(facility);

        var mainMeter = Meter.CreateMain(facility.Id, MainMeterName, GetUtcNow());
        await _meterRepository.AddAsync(mainMeter);

        return facility;
    }

    public Task<ChargingFacility?> GetFacilityAsync()
    {
        return _facilityRepository.GetAsync();
    }

    /// <summary>Registers the charger of a housing unit and creates its sub meter in the given fuse cabinet (1 or 2).</summary>
    /// <exception cref="InvalidOperationException">No facility is set up.</exception>
    /// <exception cref="ArgumentException">
    /// A value is invalid, the cabinet is not 1 or 2 (ParamName "CabinetNumber"), or the housing unit does not exist (ParamName "HousingUnitId").
    /// </exception>
    /// <exception cref="EvChargerAlreadyExistsException">The housing unit already has a charger.</exception>
    public async Task<EvCharger> RegisterChargerAsync(int housingUnitId, EvChargerDetails details, int cabinetNumber)
    {
        var facility = await GetExistingFacilityAsync();

        var housingUnit = await _housingUnitRepository.GetByIdAsync(housingUnitId);
        if (housingUnit == null)
        {
            throw new ArgumentException("The housing unit does not exist.", nameof(EvCharger.HousingUnitId));
        }

        if (await _chargerRepository.GetByHousingUnitIdAsync(housingUnitId) != null)
        {
            throw new EvChargerAlreadyExistsException(housingUnitId);
        }

        // Everything is validated before anything is stored.
        var charger = new EvCharger(facility.Id, housingUnitId, details, GetUtcNow());
        Meter.EnsureCabinetNumber(cabinetNumber);

        await _chargerRepository.AddAsync(charger);

        var subMeter = Meter.CreateSub(facility.Id, charger.Id, $"Undermåler {housingUnit.Address}", cabinetNumber, GetUtcNow());
        await _meterRepository.AddAsync(subMeter);

        return charger;
    }

    public Task<EvCharger?> GetChargerAsync(int id)
    {
        return _chargerRepository.GetByIdAsync(id);
    }

    public Task<EvCharger?> GetChargerForHousingUnitAsync(int housingUnitId)
    {
        return _chargerRepository.GetByHousingUnitIdAsync(housingUnitId);
    }

    public Task<IReadOnlyList<EvCharger>> GetChargersAsync()
    {
        return _chargerRepository.GetAllAsync();
    }

    /// <exception cref="KeyNotFoundException">No charger has the given Id.</exception>
    public async Task UpdateChargerDetailsAsync(int id, EvChargerDetails details)
    {
        var charger = await GetExistingChargerAsync(id);

        charger.UpdateDetails(details, GetUtcNow());

        await _chargerRepository.UpdateAsync(charger);
    }

    /// <exception cref="KeyNotFoundException">No charger has the given Id.</exception>
    public async Task ChangeChargerStatusAsync(int id, ChargerStatus status)
    {
        var charger = await GetExistingChargerAsync(id);

        charger.ChangeStatus(status, GetUtcNow());

        await _chargerRepository.UpdateAsync(charger);
    }

    /// <summary>Corrects the fuse cabinet of a charger's sub meter.</summary>
    /// <exception cref="KeyNotFoundException">No charger or sub meter has the given Id.</exception>
    public async Task ChangeCabinetNumberAsync(int evChargerId, int cabinetNumber)
    {
        var subMeter = await GetSubMeterAsync(evChargerId);
        if (subMeter == null)
        {
            throw new KeyNotFoundException($"Charger {evChargerId} has no sub meter.");
        }

        subMeter.ChangeCabinetNumber(cabinetNumber);

        await _meterRepository.UpdateAsync(subMeter);
    }

    public Task<IReadOnlyList<Meter>> GetMetersAsync()
    {
        return _meterRepository.GetAllAsync();
    }

    public async Task<Meter?> GetMainMeterAsync()
    {
        var meters = await _meterRepository.GetAllAsync();

        return meters.FirstOrDefault(meter => meter.IsMain);
    }

    public async Task<Meter?> GetSubMeterAsync(int evChargerId)
    {
        var meters = await _meterRepository.GetAllAsync();

        return meters.FirstOrDefault(meter => meter.EvChargerId == evChargerId);
    }

    // Readings

    /// <summary>The readings of a meter, in time order.</summary>
    public async Task<IReadOnlyList<MeterReading>> GetReadingsAsync(int meterId)
    {
        return MeterHistory.Sort(await _readingRepository.GetByMeterIdAsync(meterId));
    }

    /// <summary>
    /// Registers a reading. The first reading of a meter is its start value. Later readings must fit the
    /// chronology: not at the same time as another reading, and not lower than an earlier or higher than a later one.
    /// </summary>
    /// <exception cref="KeyNotFoundException">No meter has the given Id.</exception>
    /// <exception cref="ArgumentException">
    /// A value is invalid, or the time is in the future or already has a reading (ParamName "ReadAt", "Kwh" or "Notes").
    /// </exception>
    /// <exception cref="MeterReadingSequenceException">The value does not fit the chronology.</exception>
    public async Task<MeterReading> RegisterReadingAsync(int meterId, MeterReadingDetails details)
    {
        if (await _meterRepository.GetByIdAsync(meterId) == null)
        {
            throw new KeyNotFoundException($"Meter {meterId} was not found.");
        }

        var reading = new MeterReading(meterId, details, GetUtcNow());
        EnsureNotInTheFuture(reading.ReadAt);
        MeterHistory.EnsureFits(await _readingRepository.GetByMeterIdAsync(meterId), reading.ReadAt, reading.Kwh);

        await _readingRepository.AddAsync(reading);

        return reading;
    }

    /// <summary>
    /// Registers the main meter and the sub meters read at the same time. Every value is checked first and
    /// nothing is stored if one is invalid. Meters without a value are left out and reported as not read,
    /// never stored as zero. At least one meter must have a value. A set can be completed later by registering
    /// the missing meters with the same time.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// A value is invalid, no meter has a value, the time is in the future or a meter already has a reading
    /// at this time (ParamName "ReadAt", "MainMeterKwh", "SubMeterKwh" or "Notes").
    /// </exception>
    /// <exception cref="MeterReadingSequenceException">A value does not fit the chronology of its meter.</exception>
    /// <exception cref="InvalidOperationException">No facility is set up.</exception>
    public async Task<ReadingSetResult> RegisterReadingSetAsync(MeterReadingSetDetails details)
    {
        await GetExistingFacilityAsync();

        var meters = (await _meterRepository.GetAllAsync()).OrderBy(meter => !meter.IsMain).ThenBy(meter => meter.Id).ToList();
        var valuesByMeterId = CollectValues(details, meters);
        if (valuesByMeterId.Count == 0)
        {
            throw new ArgumentException("At least one meter must have a value.", nameof(MeterReadingSetDetails.SubMeterKwh));
        }

        // Check everything before anything is stored.
        var now = GetUtcNow();
        var readingsBefore = new Dictionary<int, IReadOnlyList<MeterReading>>();
        var newReadings = new Dictionary<int, MeterReading>();

        foreach (var meter in meters)
        {
            readingsBefore[meter.Id] = await _readingRepository.GetByMeterIdAsync(meter.Id);

            if (valuesByMeterId.TryGetValue(meter.Id, out var kwh))
            {
                var reading = CreateReading(meter, details, kwh, now);
                EnsureNotInTheFuture(reading.ReadAt);
                MeterHistory.EnsureFits(readingsBefore[meter.Id], reading.ReadAt, reading.Kwh);
                newReadings[meter.Id] = reading;
            }
        }

        await StoreAllOrNothingAsync(newReadings.Values);

        return await BuildReadingSetResultAsync(details.ReadAt, meters, readingsBefore, newReadings);
    }

    /// <summary>Corrects a reading, with the same checks as when registering. Nothing changes if a check fails.</summary>
    /// <exception cref="KeyNotFoundException">No reading has the given Id.</exception>
    public async Task CorrectReadingAsync(int readingId, MeterReadingDetails details)
    {
        var reading = await _readingRepository.GetByIdAsync(readingId);
        if (reading == null)
        {
            throw new KeyNotFoundException($"Reading {readingId} was not found.");
        }

        // A copy is checked first, so the stored reading is untouched if a check fails.
        var candidate = new MeterReading(reading.MeterId, details, GetUtcNow());
        EnsureNotInTheFuture(candidate.ReadAt);

        var others = (await _readingRepository.GetByMeterIdAsync(reading.MeterId)).Where(other => other.Id != readingId);
        MeterHistory.EnsureFits(others, candidate.ReadAt, candidate.Kwh);

        reading.Correct(details, GetUtcNow());

        await _readingRepository.UpdateAsync(reading);
    }

    /// <summary>Deletes a reading that should not exist. The remaining readings still follow the chronology.</summary>
    /// <exception cref="KeyNotFoundException">No reading has the given Id.</exception>
    public async Task DeleteReadingAsync(int readingId)
    {
        if (!await _readingRepository.DeleteAsync(readingId))
        {
            throw new KeyNotFoundException($"Reading {readingId} was not found.");
        }
    }

    // Reconciliation

    /// <summary>
    /// Compares the main meter with the sub meters in the latest period between two main meter readings.
    /// Null when the main meter has fewer than two readings.
    /// </summary>
    public async Task<ChargingReconciliation?> ReconcileLatestPeriodAsync()
    {
        var main = await GetMainMeterAsync();
        if (main == null)
        {
            return null;
        }

        var periods = ChargingReconciler.MainMeterPeriods(await _readingRepository.GetByMeterIdAsync(main.Id));
        if (periods.Count == 0)
        {
            return null;
        }

        var latest = periods[^1];

        return await ReconcileAsync(latest.From, latest.To);
    }

    /// <summary>Compares the main meter with the sub meters between two times (UTC).</summary>
    public async Task<ChargingReconciliation> ReconcileAsync(DateTime from, DateTime to, TimeSpan? tolerance = null)
    {
        var meters = await _meterRepository.GetAllAsync();
        var chargers = await _chargerRepository.GetAllAsync();
        var readings = await _readingRepository.GetAllAsync();

        return ChargingReconciler.Reconcile(meters, chargers, readings, from, to, tolerance ?? MeterHistory.DefaultTolerance);
    }

    // Helpers

    private static Dictionary<int, decimal> CollectValues(MeterReadingSetDetails details, List<Meter> meters)
    {
        var values = new Dictionary<int, decimal>();

        var main = meters.FirstOrDefault(meter => meter.IsMain);
        if (details.MainMeterKwh != null && main != null)
        {
            values[main.Id] = details.MainMeterKwh.Value;
        }

        foreach (var (meterId, kwh) in details.SubMeterKwh)
        {
            var meter = meters.FirstOrDefault(existing => existing.Id == meterId);
            if (meter == null || meter.IsMain)
            {
                throw new ArgumentException($"Meter {meterId} is not a sub meter of the facility.", nameof(MeterReadingSetDetails.SubMeterKwh));
            }

            if (kwh != null)
            {
                values[meterId] = kwh.Value;
            }
        }

        return values;
    }

    /// <summary>
    /// A reading is checked by <see cref="MeterReading"/>. Its "Kwh" error is renamed so the form can show it
    /// at the field of the main meter or of the sub meters.
    /// </summary>
    private static MeterReading CreateReading(Meter meter, MeterReadingSetDetails set, decimal kwh, DateTime now)
    {
        var details = new MeterReadingDetails { ReadAt = set.ReadAt, Kwh = kwh, Notes = set.Notes };

        try
        {
            return new MeterReading(meter.Id, details, now);
        }
        catch (ArgumentException exception) when (exception.ParamName == nameof(MeterReadingDetails.Kwh))
        {
            var field = meter.IsMain ? nameof(MeterReadingSetDetails.MainMeterKwh) : nameof(MeterReadingSetDetails.SubMeterKwh);

            throw new ArgumentException(exception.Message, field, exception);
        }
    }

    /// <summary>If storing one reading fails, the ones already stored are removed again.</summary>
    private async Task StoreAllOrNothingAsync(IEnumerable<MeterReading> readings)
    {
        var stored = new List<MeterReading>();

        try
        {
            foreach (var reading in readings)
            {
                await _readingRepository.AddAsync(reading);
                stored.Add(reading);
            }
        }
        catch (Exception storeError)
        {
            try
            {
                foreach (var reading in stored)
                {
                    await _readingRepository.DeleteAsync(reading.Id);
                }
            }
            catch (Exception cleanupError)
            {
                throw new AggregateException("Storing the readings failed, and removing the stored ones failed too.", storeError, cleanupError);
            }

            throw;
        }
    }

    private async Task<ReadingSetResult> BuildReadingSetResultAsync(
        DateTime readAt,
        List<Meter> meters,
        Dictionary<int, IReadOnlyList<MeterReading>> readingsBefore,
        Dictionary<int, MeterReading> newReadings)
    {
        var lines = new List<ReadingSetLine>();
        var notRead = new List<Meter>();

        foreach (var meter in meters)
        {
            newReadings.TryGetValue(meter.Id, out var reading);
            var previous = readingsBefore[meter.Id].Where(existing => existing.ReadAt < readAt).OrderBy(existing => existing.ReadAt).LastOrDefault();

            lines.Add(new ReadingSetLine(meter, reading, previous));
            if (reading == null)
            {
                notRead.Add(meter);
            }
        }

        ChargingReconciliation? reconciliation = null;
        var mainLine = lines.FirstOrDefault(line => line.Meter.IsMain);
        if (mainLine is { WasRead: true, PreviousReading: not null })
        {
            reconciliation = await ReconcileAsync(mainLine.PreviousReading.ReadAt, readAt);
        }

        return new ReadingSetResult { ReadAt = readAt, Lines = lines, MetersNotRead = notRead, Reconciliation = reconciliation };
    }

    private void EnsureNotInTheFuture(DateTime readAt)
    {
        if (readAt > GetUtcNow())
        {
            throw new ArgumentException("A reading cannot be in the future.", nameof(MeterReadingDetails.ReadAt));
        }
    }

    private async Task<ChargingFacility> GetExistingFacilityAsync()
    {
        var facility = await _facilityRepository.GetAsync();
        if (facility == null)
        {
            throw new InvalidOperationException("No charging facility is set up.");
        }

        return facility;
    }

    private async Task<EvCharger> GetExistingChargerAsync(int id)
    {
        var charger = await _chargerRepository.GetByIdAsync(id);
        if (charger == null)
        {
            throw new KeyNotFoundException($"Charger {id} was not found.");
        }

        return charger;
    }

    private DateTime GetUtcNow()
    {
        return _timeProvider.GetUtcNow().UtcDateTime;
    }
}
