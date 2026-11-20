using SameieHub.Domain;

namespace SameieHub.Infrastructure.Demo;

public enum DemoSeedResult
{
    Seeded,
    AlreadySeeded
}

/// <summary>What the database contains, seen from the demo command. Nothing is changed by looking.</summary>
public enum DemoDatabaseState
{
    /// <summary>No data at all. The demo can be created.</summary>
    Empty,

    /// <summary>The whole demo, including the charging facility.</summary>
    Complete,

    /// <summary>An older demo: the Fiktivdalen housing units without any charging data.</summary>
    OutdatedWithoutCharging,

    /// <summary>The demo was only partly created, or parts of it were removed or changed afterwards.</summary>
    PartlyCreated,

    /// <summary>Other data. It is never touched.</summary>
    OtherData
}

/// <summary>The database contains data that is not the Fiktivdalen demo data, so nothing is added.</summary>
public class DemoDatabaseNotEmptyException : InvalidOperationException
{
    public DemoDatabaseNotEmptyException()
        : base("The database already contains data that is not the demo data, so nothing was changed.")
    {
    }
}

/// <summary>The demo data was only partly created (for example after a crash), so it cannot be used or completed.</summary>
public class DemoDatabaseIncompleteException : InvalidOperationException
{
    public DemoDatabaseIncompleteException(Exception? innerException = null)
        : base("The demo database is only partly created. Nothing was changed. Run the demo command 'reset' to delete it and create it again.", innerException)
    {
    }
}

/// <summary>
/// The database holds an older demo without the charging facility. It is not updated automatically,
/// because the user may have changed it. A deliberate reset creates the new demo.
/// </summary>
public class DemoDatabaseOutdatedException : InvalidOperationException
{
    public DemoDatabaseOutdatedException()
        : base("The demo database is an older demo without the charging facility. Nothing was changed, and your changes are kept. "
            + "To get the new demo, stop the demo and run the demo command 'reset'. This deletes the demo database and creates it again.")
    {
    }
}

/// <summary>Where the dedicated demo database lives. It is never the ordinary database.</summary>
public static class DemoDatabaseLocation
{
    public const string FolderName = "SameieHubDemo";
    public const string FileName = "sameiehub-demo.db";

    public static string GetPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        return Path.Combine(localAppData, FolderName, FileName);
    }
}

/// <summary>
/// Fills an empty database with the fictional Fiktivdalen Sameie, using the real services and domain rules.
/// It is only run by the explicit demo command, never during normal startup.
/// All names, e-mail addresses and phone numbers are made up.
/// </summary>
public class DemoDatabaseSeeder
{
    public const string AddressPrefix = "Fiktivdalen ";

    private const int ExpectedHousingUnits = 8;
    private const int ExpectedBoardCases = 12;
    private const int ExpectedTasks = 20;

    // The last board case and the last task that are created. Together with the counts and the addresses
    // they show that the creation ran to the end, because the data is created in this order.
    private const string LastBoardCaseTitle = "Gjennomgang av ladeanleggets kapasitet";
    private const string LastTaskTitle = "Fornye merking av postkasser ved Fiktivdalen 16A";

    private static readonly string[] UnitNumbers = ["10A", "10B", "12A", "12B", "14A", "14B", "16A", "16B"];

    private readonly IHousingUnitRepository _housingUnitRepository;
    private readonly IBoardCaseRepository _boardCaseRepository;
    private readonly ITaskItemRepository _taskRepository;
    private readonly IChargingFacilityRepository _facilityRepository;
    private readonly IEvChargerRepository _chargerRepository;
    private readonly IMeterRepository _meterRepository;
    private readonly IMeterReadingRepository _readingRepository;
    private readonly TimeProvider _realClock;

    private readonly DemoClock _clock = new();
    private readonly Dictionary<string, int> _unitIds = [];
    private readonly Dictionary<string, int> _caseIds = [];
    private HousingUnitService _housingUnits = null!;
    private BoardCaseService _boardCases = null!;
    private TaskItemService _tasks = null!;
    private ChargingService _charging = null!;
    private DateOnly _today;

    public DemoDatabaseSeeder(
        IHousingUnitRepository housingUnitRepository,
        IBoardCaseRepository boardCaseRepository,
        ITaskItemRepository taskRepository,
        IChargingFacilityRepository facilityRepository,
        IEvChargerRepository chargerRepository,
        IMeterRepository meterRepository,
        IMeterReadingRepository readingRepository,
        TimeProvider timeProvider)
    {
        _housingUnitRepository = housingUnitRepository;
        _boardCaseRepository = boardCaseRepository;
        _taskRepository = taskRepository;
        _facilityRepository = facilityRepository;
        _chargerRepository = chargerRepository;
        _meterRepository = meterRepository;
        _readingRepository = readingRepository;
        _realClock = timeProvider;
    }

    /// <summary>
    /// The demo command may only run against the dedicated demo database. Both paths are made absolute and
    /// normalized (so "..", "." and letter case do not matter) before they are compared.
    /// </summary>
    public static bool IsDemoDatabasePath(string? configuredPath, string demoDatabasePath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return false;
        }

        var configuredFullPath = Path.GetFullPath(configuredPath);
        var demoFullPath = Path.GetFullPath(demoDatabasePath);

        return string.Equals(configuredFullPath, demoFullPath, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Adds the demo data to an empty database. A complete demo is left alone, and an older demo without
    /// charging data is never updated automatically.
    /// </summary>
    /// <exception cref="DemoDatabaseOutdatedException">An older demo without charging data. Nothing is changed.</exception>
    /// <exception cref="DemoDatabaseIncompleteException">The demo is only partly created. Nothing is changed.</exception>
    /// <exception cref="DemoDatabaseNotEmptyException">The database has other data. Nothing is changed.</exception>
    /// <exception cref="AggregateException">Creating the demo failed and the clean-up failed too.</exception>
    public async Task<DemoSeedResult> SeedAsync()
    {
        var state = await InspectAsync();

        switch (state)
        {
            case DemoDatabaseState.Empty:
                await CreateDemoDataSafelyAsync();

                return DemoSeedResult.Seeded;
            case DemoDatabaseState.Complete:
                return DemoSeedResult.AlreadySeeded;
            case DemoDatabaseState.OutdatedWithoutCharging:
                throw new DemoDatabaseOutdatedException();
            case DemoDatabaseState.PartlyCreated:
                throw new DemoDatabaseIncompleteException();
            default:
                throw new DemoDatabaseNotEmptyException();
        }
    }

    /// <summary>
    /// Tells what the database contains without changing anything. «Complete» means the data matches the demo
    /// content, not just that the number of rows is right. Readings registered after the demo was created do not
    /// matter, but a deleted or corrected demo reading does.
    /// </summary>
    public async Task<DemoDatabaseState> InspectAsync()
    {
        var units = await _housingUnitRepository.GetAllAsync();
        var boardCases = await _boardCaseRepository.GetAllAsync();
        var tasks = await _taskRepository.GetAllAsync();
        var facility = await _facilityRepository.GetAsync();
        var chargers = await _chargerRepository.GetAllAsync();
        var meters = await _meterRepository.GetAllAsync();
        var readings = await _readingRepository.GetAllAsync();

        var hasChargingData = facility != null || chargers.Count > 0 || meters.Count > 0 || readings.Count > 0;

        if (units.Count == 0 && boardCases.Count == 0 && tasks.Count == 0)
        {
            return hasChargingData ? DemoDatabaseState.OtherData : DemoDatabaseState.Empty;
        }

        if (IsCompleteBaseDemo(units, boardCases, tasks))
        {
            if (!hasChargingData)
            {
                return DemoDatabaseState.OutdatedWithoutCharging;
            }

            return IsCompleteChargingDemo(units, facility, chargers, meters, readings)
                ? DemoDatabaseState.Complete
                : DemoDatabaseState.PartlyCreated;
        }

        if (!hasChargingData && IsPartlyCreatedBaseDemo(units, boardCases, tasks))
        {
            return DemoDatabaseState.PartlyCreated;
        }

        if (hasChargingData && units.All(unit => unit.Address.StartsWith(AddressPrefix, StringComparison.Ordinal)))
        {
            return DemoDatabaseState.PartlyCreated;
        }

        if (!hasChargingData && HasAllDemoUnits(units))
        {
            return DemoDatabaseState.OutdatedWithoutCharging;
        }

        return DemoDatabaseState.OtherData;
    }

    private static bool HasAllDemoUnits(IReadOnlyList<HousingUnit> units)
    {
        if (units.Count != ExpectedHousingUnits)
        {
            return false;
        }

        foreach (var number in UnitNumbers)
        {
            if (!units.Any(unit => unit.Address == AddressPrefix + number))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsCompleteBaseDemo(
        IReadOnlyList<HousingUnit> units,
        IReadOnlyList<BoardCase> boardCases,
        IReadOnlyList<TaskItem> tasks)
    {
        if (boardCases.Count != ExpectedBoardCases || tasks.Count != ExpectedTasks || !HasAllDemoUnits(units))
        {
            return false;
        }

        return boardCases.Any(boardCase => boardCase.Title == LastBoardCaseTitle)
            && tasks.Any(task => task.Title == LastTaskTitle);
    }

    /// <summary>
    /// The data is created units first, then board cases, then tasks. A partly created demo therefore has
    /// only demo units and looks like the first part of that sequence.
    /// </summary>
    private static bool IsPartlyCreatedBaseDemo(
        IReadOnlyList<HousingUnit> units,
        IReadOnlyList<BoardCase> boardCases,
        IReadOnlyList<TaskItem> tasks)
    {
        if (units.Count == 0 || units.Count > ExpectedHousingUnits)
        {
            return false;
        }

        foreach (var unit in units)
        {
            if (!unit.Address.StartsWith(AddressPrefix, StringComparison.Ordinal))
            {
                return false;
            }
        }

        var onlyUnits = boardCases.Count == 0 && tasks.Count == 0;
        var allUnitsAndSomeCases = units.Count == ExpectedHousingUnits && boardCases.Count <= ExpectedBoardCases && tasks.Count == 0;
        var allUnitsAndAllCasesAndSomeTasks = units.Count == ExpectedHousingUnits
            && boardCases.Count == ExpectedBoardCases
            && tasks.Count < ExpectedTasks;

        return onlyUnits || allUnitsAndSomeCases || allUnitsAndAllCasesAndSomeTasks;
    }

    private static bool IsCompleteChargingDemo(
        IReadOnlyList<HousingUnit> units,
        ChargingFacility? facility,
        IReadOnlyList<EvCharger> chargers,
        IReadOnlyList<Meter> meters,
        IReadOnlyList<MeterReading> readings)
    {
        if (facility == null || facility.Name != DemoChargingPlan.FacilityName)
        {
            return false;
        }

        var main = meters.SingleOrDefault(meter => meter.IsMain);
        if (main == null || chargers.Count != DemoChargingPlan.Chargers.Length || meters.Count != chargers.Count + 1)
        {
            return false;
        }

        if (!ReadingsStartWith(readings, main, DemoChargingPlan.MainValues()))
        {
            return false;
        }

        foreach (var plan in DemoChargingPlan.Chargers)
        {
            var unit = units.FirstOrDefault(existing => existing.Address == AddressPrefix + plan.UnitNumber);
            var charger = unit == null ? null : chargers.FirstOrDefault(existing => existing.HousingUnitId == unit.Id);
            var subMeter = charger == null ? null : meters.FirstOrDefault(meter => meter.EvChargerId == charger.Id);

            if (charger == null
                || subMeter == null
                || charger.ChargerNumber != DemoChargingPlan.ChargerNumber(plan.UnitNumber)
                || subMeter.CabinetNumber != plan.Cabinet
                || !ReadingsStartWith(readings, subMeter, DemoChargingPlan.SubMeterValues(plan)))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// The values of the meter, oldest first, start with the planned demo values. Readings registered later come after them.
    /// </summary>
    private static bool ReadingsStartWith(IReadOnlyList<MeterReading> readings, Meter meter, List<decimal> plannedValues)
    {
        var meterReadings = MeterHistory.Sort(readings.Where(reading => reading.MeterId == meter.Id));
        if (meterReadings.Count < plannedValues.Count)
        {
            return false;
        }

        for (var index = 0; index < plannedValues.Count; index++)
        {
            if (meterReadings[index].Kwh != plannedValues[index])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// If creating fails halfway, everything that was created is removed again, so the database is empty
    /// and the command can be run again. The database was empty before the creation started.
    /// </summary>
    private async Task CreateDemoDataSafelyAsync()
    {
        try
        {
            await CreateBaseDataAsync();
        }
        catch (Exception creationError)
        {
            try
            {
                await RemoveEverythingAsync();
            }
            catch (Exception cleanupError)
            {
                throw new AggregateException(
                    "Creating the demo data failed, and the clean-up failed too. The demo database is partly created. Run the demo command 'reset'.",
                    creationError,
                    cleanupError);
            }

            throw;
        }

        // Chargers, meters and readings cannot be deleted through the repositories, on purpose. If creating them
        // fails, the demo is reported as partly created, and the demo command 'reset' deletes the demo database file.
        try
        {
            await CreateChargingDataAsync();
        }
        catch (Exception chargingError)
        {
            throw new DemoDatabaseIncompleteException(chargingError);
        }
    }

    private async Task RemoveEverythingAsync()
    {
        foreach (var task in await _taskRepository.GetAllAsync())
        {
            await _taskRepository.DeleteAsync(task.Id);
        }

        foreach (var boardCase in await _boardCaseRepository.GetAllAsync())
        {
            await _boardCaseRepository.DeleteAsync(boardCase.Id);
        }

        foreach (var unit in await _housingUnitRepository.GetAllAsync())
        {
            await _housingUnitRepository.DeleteAsync(unit.Id);
        }
    }

    private async Task CreateBaseDataAsync()
    {
        var now = _realClock.GetUtcNow();

        _clock.Set(now);
        _today = NorwegianTime.DateOf(now);
        _housingUnits = new HousingUnitService(_housingUnitRepository);
        _boardCases = new BoardCaseService(_boardCaseRepository, _housingUnitRepository, _clock);
        _tasks = new TaskItemService(_taskRepository, _housingUnitRepository, _boardCaseRepository, _clock);
        _charging = new ChargingService(_facilityRepository, _chargerRepository, _meterRepository, _readingRepository, _housingUnitRepository, _clock);

        await CreateHousingUnitsAsync();
        await CreateBoardCasesAsync();
        await CreateTasksAsync();
    }

    // Housing units: four duplexes with two sections each. The carport is described in the notes. The chargers
    // and their meters are created in CreateChargingDataAsync.

    private async Task CreateHousingUnitsAsync()
    {
        await AddUnitAsync("10A", 1, 118.5m, "Ola Eksempel", "+47 000 00 101", "ola.eksempel@example.com", "Kari Eksempel", null, "kari.eksempel@example.com",
            "Carport: én plass, nær inngangen. Fiktive demodata.");
        await AddUnitAsync("10B", 2, 112m, "Per Testesen", "+47 000 00 102", "per.testesen@example.com", null, null, null,
            "Carport: én plass. Fiktive demodata.");
        await AddUnitAsync("12A", 3, 127m, "Lise Prøve", null, "lise.prove@example.com", "Tor Prøve", null, null,
            "Carport: én plass med utebod. Fiktive demodata.");
        await AddUnitAsync("12B", 4, 95m, "Nils Demo", null, null, null, null, null,
            "Carport: én plass. Fiktive demodata.");
        await AddUnitAsync("14A", 5, 139m, "Anna Demo", "+47 000 00 105", null, "Jon Demo", "+47 000 00 205", null,
            "Carport: én plass. Fiktive demodata.");
        await AddUnitAsync("14B", 6, 105m, "Eva Fiktiv", null, "eva.fiktiv@example.com", null, null, null,
            "Carport: én plass. Fiktive demodata.");
        await AddUnitAsync("16A", 7, 145m, "Mari Fiktiv", "+47 000 00 107", "mari.fiktiv@example.com", "Svein Fiktiv", "+47 000 00 207", "svein.fiktiv@example.com",
            "Carport: én plass, største i sameiet. Fiktive demodata.");
        await AddUnitAsync("16B", 8, 101.5m, "Hans Fiktiv", "+47 000 00 108", null, null, null, null,
            "Carport: én plass. Fiktive demodata.");
    }

    private async Task AddUnitAsync(
        string number,
        int sectionNumber,
        decimal area,
        string ownerName,
        string? ownerPhone,
        string? ownerEmail,
        string? secondOwnerName,
        string? secondOwnerPhone,
        string? secondOwnerEmail,
        string notes)
    {
        _clock.SetDaysAgo(120);

        var unit = await _housingUnits.RegisterAsync(new HousingUnitDetails
        {
            Address = AddressPrefix + number,
            UnitNumber = number,
            SectionNumber = sectionNumber,
            HousingType = HousingType.Duplex,
            AreaSquareMeters = area,
            OwnerName = ownerName,
            OwnerPhoneNumber = ownerPhone,
            OwnerEmail = ownerEmail,
            SecondOwnerName = secondOwnerName,
            SecondOwnerPhoneNumber = secondOwnerPhone,
            SecondOwnerEmail = secondOwnerEmail,
            Notes = notes
        });

        _unitIds[number] = unit.Id;
    }

    // Charging facility: one main meter, eight chargers with one sub meter each, and five reading rounds.

    private async Task CreateChargingDataAsync()
    {
        _clock.SetDaysAgo(119);
        await _charging.SetUpFacilityAsync(DemoChargingPlan.FacilityName);

        _clock.SetDaysAgo(118);
        var subMeterIds = new Dictionary<string, int>();
        foreach (var plan in DemoChargingPlan.Chargers)
        {
            var details = new EvChargerDetails
            {
                ChargerNumber = DemoChargingPlan.ChargerNumber(plan.UnitNumber),
                Manufacturer = DemoChargingPlan.Manufacturer,
                Model = plan.Model,
                Status = plan.Status,
                HasElectricVehicle = plan.HasElectricVehicle,
                Notes = "Fiktive demodata."
            };

            var charger = await _charging.RegisterChargerAsync(_unitIds[plan.UnitNumber], details, plan.Cabinet);
            subMeterIds[plan.UnitNumber] = (await _charging.GetSubMeterAsync(charger.Id))!.Id;
        }

        var mainValues = DemoChargingPlan.MainValues();
        for (var round = 0; round < DemoChargingPlan.RoundDaysAgo.Length; round++)
        {
            await RegisterRoundAsync(round, mainValues[round], subMeterIds);
        }
    }

    private async Task RegisterRoundAsync(int round, decimal mainKwh, Dictionary<string, int> subMeterIds)
    {
        var readAt = ReadingTime(DemoChargingPlan.RoundDaysAgo[round]);
        var isIncompleteRound = round == DemoChargingPlan.IncompleteRoundIndex;
        var subValues = new Dictionary<int, decimal?>();
        decimal? completedLaterKwh = null;
        var completedLaterMeterId = 0;

        foreach (var plan in DemoChargingPlan.Chargers)
        {
            var kwh = AccumulatedKwh(plan, round);
            if (isIncompleteRound && plan.UnitNumber == DemoChargingPlan.NotReadUnitNumber)
            {
                continue;
            }

            if (isIncompleteRound && plan.UnitNumber == DemoChargingPlan.CompletedLaterUnitNumber)
            {
                completedLaterKwh = kwh;
                completedLaterMeterId = subMeterIds[plan.UnitNumber];
                continue;
            }

            subValues[subMeterIds[plan.UnitNumber]] = kwh;
        }

        _clock.SetUtc(readAt.AddMinutes(30));
        await _charging.RegisterReadingSetAsync(new MeterReadingSetDetails { ReadAt = readAt, MainMeterKwh = mainKwh, SubMeterKwh = subValues });

        if (completedLaterKwh != null)
        {
            // The missing reading was found and registered the next day, with the time it was read.
            _clock.SetUtc(readAt.AddDays(1));
            await _charging.RegisterReadingSetAsync(new MeterReadingSetDetails
            {
                ReadAt = readAt,
                SubMeterKwh = new Dictionary<int, decimal?> { [completedLaterMeterId] = completedLaterKwh }
            });
        }

        if (round == DemoChargingPlan.DisabledAfterRoundIndex)
        {
            _clock.SetDaysAgo(DemoChargingPlan.RoundDaysAgo[round] - 3);
            var charger = await _charging.GetChargerForHousingUnitAsync(_unitIds[DemoChargingPlan.DisabledUnitNumber]);
            await _charging.ChangeChargerStatusAsync(charger!.Id, ChargerStatus.TemporarilyDisabled);
        }
    }

    private static decimal AccumulatedKwh(DemoChargerPlan plan, int round)
    {
        var total = plan.StartKwh;
        for (var period = 0; period < round; period++)
        {
            total += plan.PeriodKwh[period];
        }

        return total;
    }

    /// <summary>08:00 Norwegian time, the given number of days before the demo is created, as UTC.</summary>
    private DateTime ReadingTime(int daysAgo)
    {
        var local = _today.AddDays(-daysAgo).ToDateTime(new TimeOnly(8, 0), DateTimeKind.Unspecified);
        NorwegianTime.TryConvertToUtc(local, null, out var utc, out _);

        return utc;
    }

    // Board cases. Dates are relative to today, so the demo always has past and upcoming meetings.

    private async Task CreateBoardCasesAsync()
    {
        await AddCaseAsync("uteomrade", "Vedlikehold av felles uteområde",
            "Plen, hekker og beplantning i fellesarealene trenger fast stell gjennom sesongen.",
            BoardCasePriority.Normal, createdDaysAgo: 75, meetingInDays: -60, unit: null,
            "Se tilbud fra gartner i saksmappen (fiktiv).",
            decision: "Vedtatt å leie inn gartner to ganger i sesongen, innenfor budsjettert ramme.", decisionInDays: -60, status: BoardCaseStatus.Decided);

        await AddCaseAsync("elbilservice", "Service på elbilladeanlegget",
            "Ladeanlegget har ikke vært kontrollert siden installasjonen. Én lader er midlertidig deaktivert.",
            BoardCasePriority.High, 30, 9, null,
            "Innhent tilbud på årlig serviceavtale. Se også gjennomgang av kapasitet.",
            status: BoardCaseStatus.InProgress, statusDaysAgo: 24);

        await AddCaseAsync("carporter", "Inspeksjon av carporter",
            "Årlig kontroll av tak, stolper og takrenner på alle åtte carportene.",
            BoardCasePriority.Normal, 50, -35, null,
            null,
            decision: "Vedtatt at nestleder gjennomfører befaring, og at funn rapporteres til styret.", decisionInDays: -35, status: BoardCaseStatus.Decided);

        await AddCaseAsync("belysning", "Utskifting av fellesbelysning",
            "Flere av lampene langs gangveiene er defekte, og eksisterende armaturer bruker mye strøm.",
            BoardCasePriority.High, 40, -5, null,
            "Styret ønsker LED og sensorstyring. Pristilbud mangler fortsatt.",
            status: BoardCaseStatus.InProgress, statusDaysAgo: 36);

        await AddCaseAsync("arsmote", "Forberedelse til årlig sameiemøte",
            "Innkalling, saksliste og årsberetning må være klar i god tid før møtet.",
            BoardCasePriority.High, 3, 21, null, null);

        await AddCaseAsync("gjerde", "Reparasjon av felles gjerde",
            "Gjerdet mot veien var skadet etter en storm.",
            BoardCasePriority.Normal, 110, -95, null,
            "Saken er ferdigbehandlet.",
            decision: "Vedtatt å reparere gjerdet og male stolpene. Arbeidet er utført.", decisionInDays: -95, status: BoardCaseStatus.Closed, statusDaysAgo: 80);

        await AddCaseAsync("snoryddig", "Snørydding og vintervedlikehold",
            "Avtale om brøyting av felles vei og parkering, og strøing av gangveier.",
            BoardCasePriority.Normal, 20, -10, null,
            null,
            decision: "Vedtatt å inngå sesongavtale om brøyting. Strøing gjøres på dugnad.", decisionInDays: -10, status: BoardCaseStatus.Decided);

        await AddCaseAsync("drenering", "Kontroll av drenering",
            "Vann blir stående ved enkelte nedløp etter kraftig regn. Dreneringen bør kontrolleres.",
            BoardCasePriority.Normal, 8, 30, null, null);

        await AddCaseAsync("gjesteparkering", "Oppmerking av gjesteparkering",
            "Gjesteparkeringen mangler tydelig oppmerking, og plassene brukes tilfeldig.",
            BoardCasePriority.Low, 90, -80, null,
            null,
            decision: "Vedtatt å merke fire gjesteplasser. Utført.", decisionInDays: null, status: BoardCaseStatus.Closed, statusDaysAgo: 70);

        await AddCaseAsync("vedlikeholdsplan", "Oppdatering av felles vedlikeholdsplan",
            "Planen for kommende år bør oppdateres med nye behov og erfaringer.",
            BoardCasePriority.Low, 14, 45, null,
            "Utkast deles med eierne før sameiemøtet.",
            status: BoardCaseStatus.InProgress, statusDaysAgo: 10);

        await AddCaseAsync("lekkasje", "Henvendelse om lekkasje ved Fiktivdalen 12B",
            "Eier melder om fuktskader i vegg mot carporten etter regnvær.",
            BoardCasePriority.High, 4, 6, "12B",
            "Eier har sendt bilder (fiktive). Rørlegger kontaktes.",
            status: BoardCaseStatus.InProgress, statusDaysAgo: 3);

        await AddCaseAsync("kapasitet", LastBoardCaseTitle,
            "Flere ønsker å skaffe elbil. Er hovedsikringen og fordelingen dimensjonert for åtte ladere i bruk samtidig?",
            BoardCasePriority.Normal, 6, 16, null, null);
    }

    private async Task AddCaseAsync(
        string key,
        string title,
        string description,
        BoardCasePriority priority,
        int createdDaysAgo,
        int meetingInDays,
        string? unit,
        string? notes,
        string? decision = null,
        int? decisionInDays = null,
        BoardCaseStatus status = BoardCaseStatus.New,
        int statusDaysAgo = 0)
    {
        _clock.SetDaysAgo(createdDaysAgo);

        var boardCase = await _boardCases.RegisterAsync(new BoardCaseDetails
        {
            Title = title,
            Description = description,
            Priority = priority,
            MeetingDate = _today.AddDays(meetingInDays),
            HousingUnitId = unit == null ? null : _unitIds[unit],
            Notes = notes
        });
        _caseIds[key] = boardCase.Id;

        if (decision != null)
        {
            _clock.SetDaysAgo(decisionInDays == null ? -meetingInDays : -decisionInDays.Value);
            var decisionDate = decisionInDays == null ? (DateOnly?)null : _today.AddDays(decisionInDays.Value);
            await _boardCases.RegisterDecisionAsync(boardCase.Id, decision, decisionDate);
        }

        if (status != BoardCaseStatus.New)
        {
            _clock.SetDaysAgo(statusDaysAgo == 0 ? Math.Max(0, -meetingInDays) : statusDaysAgo);
            await _boardCases.ChangeStatusAsync(boardCase.Id, status);
        }
    }

    // Tasks

    private async Task CreateTasksAsync()
    {
        await AddTaskAsync("Innhente pristilbud på LED-fellesbelysning", TaskItemCategory.Maintenance, TaskItemPriority.High,
            createdDaysAgo: 35, dueInDays: -3, assignedTo: "Lise Prøve", caseKey: "belysning", status: TaskItemStatus.InProgress, statusDaysAgo: 20,
            description: "Minst to tilbud, med pris for armaturer og montering.");
        await AddTaskAsync("Bestille servicebesøk for elbilladerne", TaskItemCategory.Maintenance, TaskItemPriority.High,
            25, 4, "Ola Eksempel", caseKey: "elbilservice");
        await AddTaskAsync("Kontrollere ladepunktet ved Fiktivdalen 14A", TaskItemCategory.Maintenance, TaskItemPriority.High,
            20, 0, "Svein Fiktiv", unit: "14A", caseKey: "elbilservice", status: TaskItemStatus.InProgress, statusDaysAgo: 6,
            description: "Laderen er midlertidig deaktivert. Finn årsaken sammen med elektriker.");
        await AddTaskAsync("Sende innkalling til årlig sameiemøte", TaskItemCategory.General, TaskItemPriority.High,
            2, 7, "Ola Eksempel", caseKey: "arsmote");
        await AddTaskAsync("Samle nøkkeltall og saker til årsberetningen", TaskItemCategory.General, TaskItemPriority.Normal,
            2, 18, "Lise Prøve", caseKey: "arsmote",
            notes: "Hent vedtak fra styresakene.");
        await AddTaskAsync("Befaring av drenering ved Fiktivdalen 10B", TaskItemCategory.Maintenance, TaskItemPriority.Normal,
            7, 0, null, unit: "10B", caseKey: "drenering");
        await AddTaskAsync("Tømme sluk og takrenner før frost", TaskItemCategory.Maintenance, TaskItemPriority.Normal,
            30, -8, null);
        await AddTaskAsync("Rydde opp i lageret for strøgrus", TaskItemCategory.Maintenance, TaskItemPriority.Low,
            15, null, null);
        await AddTaskAsync("Bestille vinterbrøyting", TaskItemCategory.Maintenance, TaskItemPriority.High,
            22, -14, "Svein Fiktiv", caseKey: "snoryddig", status: TaskItemStatus.Completed, statusDaysAgo: 12);
        await AddTaskAsync("Inspisere tak og takrenner på carportene", TaskItemCategory.Maintenance, TaskItemPriority.Normal,
            52, -30, "Nils Demo", caseKey: "carporter", status: TaskItemStatus.Completed, statusDaysAgo: 34,
            notes: "Ingen alvorlige funn. Mindre rust på to takrenner.");
        await AddTaskAsync("Male gjerdestolper", TaskItemCategory.Maintenance, TaskItemPriority.Normal,
            108, -90, "Ola Eksempel", caseKey: "gjerde", status: TaskItemStatus.Completed, statusDaysAgo: 92);
        await AddTaskAsync("Merke gjesteparkeringsplassene", TaskItemCategory.General, TaskItemPriority.Low,
            88, -82, "Lise Prøve", caseKey: "gjesteparkering", status: TaskItemStatus.Completed, statusDaysAgo: 84);
        await AddTaskAsync("Midlertidig tetting av lekkasje ved Fiktivdalen 12B", TaskItemCategory.Maintenance, TaskItemPriority.High,
            4, 1, "Rørlegger (fiktiv)", unit: "12B", caseKey: "lekkasje", status: TaskItemStatus.InProgress, statusDaysAgo: 2);
        await AddTaskAsync("Dokumentere fuktskade ved Fiktivdalen 12B", TaskItemCategory.General, TaskItemPriority.High,
            3, -1, "Nils Demo", unit: "12B", status: TaskItemStatus.InProgress, statusDaysAgo: 2,
            description: "Ta bilder og notér målinger til forsikring og rørlegger.");
        await AddTaskAsync("Oppdatere kontaktlisten for styret", TaskItemCategory.General, TaskItemPriority.Normal,
            10, 35, "Lise Prøve");
        await AddTaskAsync("Gå gjennom vedlikeholdsplanen for neste år", TaskItemCategory.Maintenance, TaskItemPriority.Low,
            12, 55, "Ola Eksempel", caseKey: "vedlikeholdsplan");
        await AddTaskAsync("Måle belastning på hovedsikringen med ladere i bruk", TaskItemCategory.Maintenance, TaskItemPriority.Normal,
            5, 40, null, caseKey: "kapasitet");
        await AddTaskAsync("Montere ny belysning ved carportene", TaskItemCategory.Maintenance, TaskItemPriority.Normal,
            30, null, null, caseKey: "belysning");
        await AddTaskAsync("Rydde fellesområdet etter dugnad", TaskItemCategory.General, TaskItemPriority.Low,
            30, -20, "Dugnadsgruppen", status: TaskItemStatus.Completed, statusDaysAgo: 21);
        await AddTaskAsync(LastTaskTitle, TaskItemCategory.General, TaskItemPriority.Low,
            9, -2, null, unit: "16A");
    }

    private async Task AddTaskAsync(
        string title,
        TaskItemCategory category,
        TaskItemPriority priority,
        int createdDaysAgo,
        int? dueInDays,
        string? assignedTo,
        string? unit = null,
        string? caseKey = null,
        TaskItemStatus status = TaskItemStatus.NotStarted,
        int statusDaysAgo = 0,
        string? description = null,
        string? notes = null)
    {
        _clock.SetDaysAgo(createdDaysAgo);

        var task = await _tasks.RegisterAsync(new TaskItemDetails
        {
            Title = title,
            Description = description,
            Category = category,
            Priority = priority,
            DueDate = dueInDays == null ? null : _today.AddDays(dueInDays.Value),
            AssignedTo = assignedTo,
            HousingUnitId = unit == null ? null : _unitIds[unit],
            BoardCaseId = caseKey == null ? null : _caseIds[caseKey],
            Notes = notes
        });

        if (status != TaskItemStatus.NotStarted)
        {
            _clock.SetDaysAgo(statusDaysAgo);
            await _tasks.ChangeStatusAsync(task.Id, status);
        }
    }

    /// <summary>
    /// A clock the seeder moves back in time, so the demo data gets creation and completion times
    /// spread over the last months. Nothing is ever dated after the real current time.
    /// </summary>
    private sealed class DemoClock : TimeProvider
    {
        private DateTimeOffset _realNow;
        private DateTimeOffset _current;

        public void Set(DateTimeOffset realNow)
        {
            _realNow = realNow;
            _current = realNow;
        }

        public void SetDaysAgo(int days)
        {
            _current = _realNow.AddDays(-days);
        }

        public void SetUtc(DateTime utc)
        {
            _current = new DateTimeOffset(utc, TimeSpan.Zero);
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _current;
        }
    }
}
