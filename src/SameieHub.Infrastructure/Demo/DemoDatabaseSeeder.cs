using SameieHub.Domain;

namespace SameieHub.Infrastructure.Demo;

public enum DemoSeedResult
{
    Seeded,
    AlreadySeeded
}

/// <summary>The database contains data that is not the Fiktivdalen demo data, so nothing is added.</summary>
public class DemoDatabaseNotEmptyException : InvalidOperationException
{
    public DemoDatabaseNotEmptyException()
        : base("The database already contains data that is not demo data, so the demo data was not added.")
    {
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

    private readonly IHousingUnitRepository _housingUnitRepository;
    private readonly IBoardCaseRepository _boardCaseRepository;
    private readonly ITaskItemRepository _taskRepository;
    private readonly TimeProvider _realClock;

    private readonly DemoClock _clock = new();
    private readonly Dictionary<string, int> _unitIds = [];
    private readonly Dictionary<string, int> _caseIds = [];
    private HousingUnitService _housingUnits = null!;
    private BoardCaseService _boardCases = null!;
    private TaskItemService _tasks = null!;
    private DateOnly _today;

    public DemoDatabaseSeeder(
        IHousingUnitRepository housingUnitRepository,
        IBoardCaseRepository boardCaseRepository,
        ITaskItemRepository taskRepository,
        TimeProvider timeProvider)
    {
        _housingUnitRepository = housingUnitRepository;
        _boardCaseRepository = boardCaseRepository;
        _taskRepository = taskRepository;
        _realClock = timeProvider;
    }

    /// <summary>
    /// The demo command must be given an explicit database file that is not the ordinary one.
    /// </summary>
    public static bool IsSafeDemoPath(string? configuredPath, string defaultPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return false;
        }

        var configuredFullPath = Path.GetFullPath(configuredPath);
        var defaultFullPath = Path.GetFullPath(defaultPath);

        return !string.Equals(configuredFullPath, defaultFullPath, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Adds the demo data to an empty database. When the demo data is already there, nothing happens.
    /// </summary>
    /// <exception cref="DemoDatabaseNotEmptyException">The database has other data. Nothing is changed.</exception>
    public async Task<DemoSeedResult> SeedAsync()
    {
        var existingUnits = await _housingUnitRepository.GetAllAsync();
        var existingCases = await _boardCaseRepository.GetAllAsync();
        var existingTasks = await _taskRepository.GetAllAsync();

        if (existingUnits.Count == 0 && existingCases.Count == 0 && existingTasks.Count == 0)
        {
            await CreateDemoDataAsync();

            return DemoSeedResult.Seeded;
        }

        if (IsOnlyDemoUnits(existingUnits))
        {
            return DemoSeedResult.AlreadySeeded;
        }

        throw new DemoDatabaseNotEmptyException();
    }

    private static bool IsOnlyDemoUnits(IReadOnlyList<HousingUnit> units)
    {
        if (units.Count == 0)
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

        return true;
    }

    private async Task CreateDemoDataAsync()
    {
        var now = _realClock.GetUtcNow();

        _clock.Set(now);
        _today = NorwegianTime.DateOf(now);
        _housingUnits = new HousingUnitService(_housingUnitRepository);
        _boardCases = new BoardCaseService(_boardCaseRepository, _housingUnitRepository, _clock);
        _tasks = new TaskItemService(_taskRepository, _housingUnitRepository, _boardCaseRepository, _clock);

        await CreateHousingUnitsAsync();
        await CreateBoardCasesAsync();
        await CreateTasksAsync();
    }

    // Housing units: four duplexes with two sections each. Carport and charger are described in the notes,
    // because the housing unit has no fields for them yet.

    private async Task CreateHousingUnitsAsync()
    {
        await AddUnitAsync("10A", 1, 118.5m, "Ola Eksempel", "+47 000 00 101", "ola.eksempel@example.com", "Kari Eksempel", null, "kari.eksempel@example.com",
            "Carport: én plass, nær inngangen. Elbillader: installert og i bruk (husstanden har elbil). Fiktive demodata.");
        await AddUnitAsync("10B", 2, 112m, "Per Testesen", "+47 000 00 102", "per.testesen@example.com", null, null, null,
            "Carport: én plass. Elbillader: installert. Ingen elbil registrert i husstanden. Fiktive demodata.");
        await AddUnitAsync("12A", 3, 127m, "Lise Prøve", null, "lise.prove@example.com", "Tor Prøve", null, null,
            "Carport: én plass med utebod. Elbillader: installert og i bruk. Fiktive demodata.");
        await AddUnitAsync("12B", 4, 95m, "Nils Demo", null, null, null, null, null,
            "Carport: én plass. Elbillader: installert, men ikke i bruk (eier vurderer elbil). Fiktive demodata.");
        await AddUnitAsync("14A", 5, 139m, "Anna Demo", "+47 000 00 105", null, "Jon Demo", "+47 000 00 205", null,
            "Carport: én plass. Elbillader: installert, men midlertidig deaktivert i påvente av service. Fiktive demodata.");
        await AddUnitAsync("14B", 6, 105m, "Eva Fiktiv", null, "eva.fiktiv@example.com", null, null, null,
            "Carport: én plass. Elbillader: installert. Ingen elbil registrert i husstanden. Fiktive demodata.");
        await AddUnitAsync("16A", 7, 145m, "Mari Fiktiv", "+47 000 00 107", "mari.fiktiv@example.com", "Svein Fiktiv", "+47 000 00 207", "svein.fiktiv@example.com",
            "Carport: én plass, største i sameiet. Elbillader: installert og i bruk (to elbiler i husstanden). Fiktive demodata.");
        await AddUnitAsync("16B", 8, 101.5m, "Hans Fiktiv", "+47 000 00 108", null, null, null, null,
            "Carport: én plass. Elbillader: installert og i bruk. Fiktive demodata.");
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

        await AddCaseAsync("kapasitet", "Gjennomgang av ladeanleggets kapasitet",
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
        await AddTaskAsync("Fornye merking av postkasser ved Fiktivdalen 16A", TaskItemCategory.General, TaskItemPriority.Low,
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

        public override DateTimeOffset GetUtcNow()
        {
            return _current;
        }
    }
}
