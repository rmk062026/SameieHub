using Microsoft.EntityFrameworkCore;
using SameieHub.Domain;

namespace SameieHub.Infrastructure.Persistence;

// The four charging repositories follow the other repositories: each method uses its own short-lived DbContext,
// so objects returned here are never tracked by a context. Where more than one row must be stored together,
// the method opens an explicit database transaction. If anything fails, the transaction is not committed and
// SQLite rolls everything back.

public class EfChargingFacilityRepository : IChargingFacilityRepository
{
    private readonly IDbContextFactory<SameieHubDbContext> _contextFactory;

    public EfChargingFacilityRepository(IDbContextFactory<SameieHubDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task AddWithMainMeterAsync(ChargingFacility facility, Func<int, Meter> createMainMeter)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        await using var transaction = await context.Database.BeginTransactionAsync();

        try
        {
            context.ChargingFacilities.Add(facility);
            await context.SaveChangesAsync();

            context.Meters.Add(createMainMeter(facility.Id));
            await context.SaveChangesAsync();

            await transaction.CommitAsync();
        }
        catch (DbUpdateException exception) when (SqliteConstraintErrors.IsCheckViolation(exception, SameieHubDbContext.OneFacilityConstraint))
        {
            throw new ChargingFacilityAlreadyExistsException();
        }
    }

    public async Task<ChargingFacility?> GetAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        return await context.ChargingFacilities.AsNoTracking().OrderBy(facility => facility.Id).FirstOrDefaultAsync();
    }
}

public class EfEvChargerRepository : IEvChargerRepository
{
    private readonly IDbContextFactory<SameieHubDbContext> _contextFactory;

    public EfEvChargerRepository(IDbContextFactory<SameieHubDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task AddWithSubMeterAsync(EvCharger charger, Func<int, Meter> createSubMeter)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        await using var transaction = await context.Database.BeginTransactionAsync();

        try
        {
            context.EvChargers.Add(charger);
            await context.SaveChangesAsync();

            context.Meters.Add(createSubMeter(charger.Id));
            await context.SaveChangesAsync();

            await transaction.CommitAsync();
        }
        catch (DbUpdateException exception) when (SqliteConstraintErrors.IsUniqueViolation(exception, "EvChargers.HousingUnitId"))
        {
            throw new EvChargerAlreadyExistsException(charger.HousingUnitId, exception);
        }
    }

    public async Task<EvCharger?> GetByIdAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        return await context.EvChargers.AsNoTracking().FirstOrDefaultAsync(charger => charger.Id == id);
    }

    public async Task<EvCharger?> GetByHousingUnitIdAsync(int housingUnitId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        return await context.EvChargers.AsNoTracking().FirstOrDefaultAsync(charger => charger.HousingUnitId == housingUnitId);
    }

    public async Task<IReadOnlyList<EvCharger>> GetAllAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        return await context.EvChargers.AsNoTracking().OrderBy(charger => charger.Id).ToListAsync();
    }

    public async Task UpdateAsync(EvCharger charger)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        // The charger was loaded by another context, so Update tells this one to save all its values.
        context.EvChargers.Update(charger);
        await context.SaveChangesAsync();
    }
}

public class EfMeterRepository : IMeterRepository
{
    private readonly IDbContextFactory<SameieHubDbContext> _contextFactory;

    public EfMeterRepository(IDbContextFactory<SameieHubDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task AddAsync(Meter meter)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        context.Meters.Add(meter);
        await context.SaveChangesAsync();
    }

    public async Task<Meter?> GetByIdAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        return await context.Meters.AsNoTracking().FirstOrDefaultAsync(meter => meter.Id == id);
    }

    public async Task<IReadOnlyList<Meter>> GetAllAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        return await context.Meters.AsNoTracking().OrderBy(meter => meter.Id).ToListAsync();
    }

    public async Task UpdateAsync(Meter meter)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        context.Meters.Update(meter);
        await context.SaveChangesAsync();
    }
}

public class EfMeterReadingRepository : IMeterReadingRepository
{
    private const string DuplicateTimeIndex = "MeterReadings.MeterId, MeterReadings.ReadAt";

    private readonly IDbContextFactory<SameieHubDbContext> _contextFactory;

    public EfMeterReadingRepository(IDbContextFactory<SameieHubDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task AddAsync(MeterReading reading)
    {
        await AddRangeAsync([reading]);
    }

    public async Task AddRangeAsync(IReadOnlyList<MeterReading> readings)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        await using var transaction = await context.Database.BeginTransactionAsync();

        try
        {
            context.MeterReadings.AddRange(readings);
            await context.SaveChangesAsync();

            await transaction.CommitAsync();
        }
        catch (DbUpdateException exception) when (SqliteConstraintErrors.IsUniqueViolation(exception, DuplicateTimeIndex))
        {
            throw DuplicateTimeException(exception);
        }
    }

    public async Task<MeterReading?> GetByIdAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        return await context.MeterReadings.AsNoTracking().FirstOrDefaultAsync(reading => reading.Id == id);
    }

    public async Task<IReadOnlyList<MeterReading>> GetByMeterIdAsync(int meterId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        return await context.MeterReadings
            .AsNoTracking()
            .Where(reading => reading.MeterId == meterId)
            .OrderBy(reading => reading.Id)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<MeterReading>> GetAllAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        return await context.MeterReadings.AsNoTracking().OrderBy(reading => reading.Id).ToListAsync();
    }

    public async Task UpdateAsync(MeterReading reading)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        context.MeterReadings.Update(reading);

        try
        {
            await context.SaveChangesAsync();
        }
        catch (DbUpdateException exception) when (SqliteConstraintErrors.IsUniqueViolation(exception, DuplicateTimeIndex))
        {
            throw DuplicateTimeException(exception);
        }
    }

    public async Task<bool> DeleteAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var reading = await context.MeterReadings.FirstOrDefaultAsync(existing => existing.Id == id);
        if (reading == null)
        {
            return false;
        }

        context.MeterReadings.Remove(reading);
        await context.SaveChangesAsync();

        return true;
    }

    /// <summary>The service checks for duplicate times first. This is the same error when the database catches it.</summary>
    private static ArgumentException DuplicateTimeException(DbUpdateException exception)
    {
        return new ArgumentException("The meter already has a reading at this time.", nameof(MeterReadingDetails.ReadAt), exception);
    }
}
