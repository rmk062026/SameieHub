# Kodegjennomgang

Dette dokumentet hjelper deg å **lese** den faktiske C#-koden i SameieHub. Alle kodeutdrag er hentet fra repoet (filstien står over hvert utdrag). Hvor ting ligger, står i [PROJECT_STRUCTURE.md](PROJECT_STRUCTURE.md). Hvorfor strukturen er som den er, står i [ARCHITECTURE_EXPLAINED.md](ARCHITECTURE_EXPLAINED.md).

Kommentarer som er kuttet ut av utdragene for å spare plass, er markert med `// …`. Kommentarene i kildekoden gjentas ikke her; i stedet forklares *hvorfor* og *hva alternativene er*.

Innhold: [1 Typer](#1-class-interface-record-og-enum) · [2 Properties](#2-properties-og-private-set) · [3 Konstruktører](#3-konstruktører) · [4 Validering](#4-validering-og-domeneregler) · [5 Nullable](#5-nullable-typer) · [6 Tjenester og DI](#6-tjenester-og-dependency-injection) · [7 async/await](#7-async-await-task-og-taskt) · [8 EF Core](#8-ef-core-mapping-og-repository) · [9 Exceptions](#9-exception-håndtering) · [10 LINQ](#10-linq) · [11 Testdobler og tester](#11-test-doubles-og-tester) · [12 Razor-skjema](#12-et-razor-skjema)

---

## 1. `class`, `interface`, `record` og `enum`

SameieHub bruker alle fire. Hver har en bestemt jobb:

| Type | Brukes til | Eksempel i repoet |
|---|---|---|
| `class` | Objekter med identitet og atferd (entiteter, tjenester, repositories) | `HousingUnit`, `TaskItemService`, `EfTaskItemRepository` |
| `interface` | En kontrakt uten implementasjon | `IHousingUnitRepository` |
| `record` | Et bunt verdier der **innholdet** er det viktige | `TaskItemDetails`, `MeterReadingDetails`, `UpcomingDate` |
| `enum` | Et fast sett navngitte valg | `TaskItemStatus`, `ChargerStatus`, `HousingType` |

### `record`: `src/SameieHub.Domain/TaskItemDetails.cs`
```csharp
public record TaskItemDetails
{
    public required string Title { get; init; }
    public string? Description { get; init; }
    public TaskItemCategory Category { get; init; } = TaskItemCategory.General;
    public TaskItemPriority Priority { get; init; } = TaskItemPriority.Normal;
    public DateOnly? DueDate { get; init; }
    // … AssignedTo, HousingUnitId, BoardCaseId, Notes
}
```
- `init` betyr at egenskapen kan settes når objektet opprettes (`new TaskItemDetails { Title = "…" }`), men ikke etterpå.
- `required` tvinger den som lager objektet til å oppgi tittel. Kompilatoren stopper deg ellers.
- Standardverdier (`= TaskItemCategory.General`) gjør at skjemaet bare trenger å sette det som avviker.
- To records med samme verdier er **like** (`==` sammenligner innholdet). Dette brukes bevisst i `EvCharger.UpdateDetails`: `if (ToDetails() != before)` sier «har noe faktisk endret seg?» uten å sammenligne felt for felt.
- `with` lager en kopi med én endring: `task.ToDetails() with { HousingUnitId = housingUnitId }` (i `TaskItemService.ChangeHousingUnitAsync`).

**Hvorfor record og ikke class her?** Detaljene er «bare data» som skal sendes fra skjema til tjeneste. De trenger ikke regler eller identitet. Alternativet var å sende 9–12 enkeltparametere til hver metode, som er uleselig og lett å blande rekkefølgen på.

### `enum`: `src/SameieHub.Domain/TaskItemStatus.cs`
```csharp
public enum TaskItemStatus { NotStarted = 1, InProgress = 2, Completed = 3 }
```
Verdiene starter på 1 slik at standardverdien `0` aldri betyr en gyldig status ved et uhell. `Enum.IsDefined(status)` brukes for å avvise tall som ikke er en av verdiene. I databasen lagres enum som tekst (`HasConversion<string>()`), slik at tabellen er lesbar (`"InProgress"`), og slik at rekkefølgen i enum ikke betyr noe for lagrede data.

### `interface`: `src/SameieHub.Domain/IHousingUnitRepository.cs`
```csharp
public interface IHousingUnitRepository
{
    Task AddAsync(HousingUnit unit);
    Task<HousingUnit?> GetByIdAsync(int id);
    Task<IReadOnlyList<HousingUnit>> GetAllAsync();
    Task UpdateAsync(HousingUnit unit);
    Task<bool> DeleteAsync(int id);
}
```
Her står bare *hva*, aldri *hvordan*. Det lar `HousingUnitService` fungere likt enten dataene ligger i SQLite (`EfHousingUnitRepository`) eller i en `Dictionary` i en test (`FakeHousingUnitRepository`). `IReadOnlyList<T>` forteller at den som får listen ikke skal endre den.

---

## 2. Properties og `private set`

`src/SameieHub.Domain/HousingUnit.cs`:
```csharp
public int Id { get; private set; }
public string Address { get; private set; }
public string? UnitNumber { get; private set; }
public bool IsActive { get; private set; } = true;
```
En *property* er en «smart variabel» med `get` (les) og `set` (skriv). `private set` betyr at bare koden **inne i klassen** kan endre verdien. Utenfra kan du lese `unit.Address`, men ikke skrive `unit.Address = ""`.

**Hvorfor?** Hvis hvem som helst kunne sette `Address`, kunne en tom adresse snike seg inn. Med `private set` må man gå via `ChangeAddress` eller `UpdateDetails`, som alltid validerer. Det er hele poenget med et *domenemodell*-objekt: det kan ikke bringes i en ugyldig tilstand utenfra.

Alternativet er «anemiske» klasser med åpne `{ get; set; }` og regler et annet sted. Det er enklere å skrive, men reglene kan omgås og må gjentas.

---

## 3. Konstruktører

```csharp
public HousingUnit(string address)
{
    Address = NormalizeAddress(address);
}

public HousingUnit(HousingUnitDetails details)
    : this(details.Address)
{
    UpdateDetails(details);
}
```
- En konstruktør kjører når du skriver `new HousingUnit(...)`. Her validerer den, så **et `HousingUnit`-objekt med ugyldig adresse kan ikke eksistere**.
- `: this(details.Address)` betyr «kall den andre konstruktøren først». Den første linjen validerer adressen, og deretter fyller `UpdateDetails` inn resten. Reglene ligger dermed på ett sted.

### Den private konstruktøren i `TaskItem`
```csharp
// Used by EF Core when it reads a task from the database.
private TaskItem()
{
}
```
EF Core trenger å lage objektet selv når det leser en rad, og kan bruke en privat parameterløs konstruktør og deretter fylle egenskapene (også de med `private set`). Utenfra kan ingen bruke den, så reglene i den offentlige konstruktøren omgås ikke.

### `AssignId`
```csharp
public void AssignId(int id)
{
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);
    if (Id != 0) throw new InvalidOperationException($"Housing unit already has Id {Id}.");
    Id = id;
}
```
Databasen deler ut Id-en. I minnet (i testene) gjør `FakeHousingUnitRepository` det samme via `AssignId`. Metoden er `public` fordi repositories ligger i andre prosjekter, men den kan bare brukes **én gang** og bare med positivt tall, slik at Id-en ikke kan byttes ut senere.

---

## 4. Validering og domeneregler

Her er kjernen i `HousingUnit.UpdateDetails` (`src/SameieHub.Domain/HousingUnit.cs`), forkortet:
```csharp
public void UpdateDetails(HousingUnitDetails details)
{
    // Validate everything first so an invalid value cannot leave the unit half updated.
    var address = NormalizeAddress(details.Address, nameof(details.Address));
    var unitNumber = NormalizeOptionalText(details.UnitNumber, MaxUnitNumberLength, nameof(details.UnitNumber));
    var sectionNumber = ValidateSectionNumber(details.SectionNumber);
    // … flere linjer …
    Address = address;
    UnitNumber = unitNumber;
    SectionNumber = sectionNumber;
    // … tilordning av resten …
}
```

**Hva skjer, steg for steg?**
1. Hver `Normalize…`/`Validate…`-metode får en verdi og enten returnerer en «renset» verdi (trimmet tekst, `null` hvis blank) eller kaster `ArgumentException`.
2. Hvis **en** av dem kaster, avbrytes metoden *før* noen egenskap er tilordnet. Objektet er da helt uendret.
3. Først når alle er godkjent, tilordnes egenskapene. Dette kalles «alt eller ingenting» (atomisk oppdatering).

**Hvorfor `nameof(details.UnitNumber)` som feltnavn?** `ArgumentException` har egenskapen `ParamName`. Ved å sette den til egenskapsnavnet kan skjemaet slå opp riktig norsk melding (`HousingUnitFieldMessages`) og vise den ved riktig felt. Det er en liten kontrakt mellom Domain og Web, og den er testet.

**Alternativer:** `DataAnnotations` (`[MaxLength(200)]`) på entiteten ville vært kortere, men legger reglene i attributter som bare virker i frameworks som leser dem, og gir mindre kontroll (f.eks. «blank tekst blir null»). Et valideringsbibliotek (som FluentValidation) er et godt alternativ i større prosjekter, men ble ikke tatt inn (ingen nye pakker uten god grunn).

### «Er noe endret?»: `TaskItem.UpdateDetails`
```csharp
var isUnchanged = title == Title
    && description == Description
    && category == Category
    // … flere sammenligninger …
if (isUnchanged)
{
    return;
}
// … tilordning …
UpdatedAt = now;
```
`UpdatedAt` settes bare når noe virkelig har endret seg. Uten sjekken ville å trykke «Lagre» uten endring likevel flyttet «sist oppdatert».

### Regler som er tid-avhengige
```csharp
public bool IsOverdue(DateOnly today)
{
    return Status != TaskItemStatus.Completed && DueDate < today;
}
```
- `DueDate` er `DateOnly?` (kan være null). Sammenligningen `null < today` er `false` i C# for nullable-typer, så en oppgave uten frist er aldri forfalt.
- `today` sendes **inn** i stedet for at metoden spør klokken selv. Da kan testene prøve «dagen etter fristen» uten å vente (se `IsOverdue_DueToday_IsFalseUntilTheNextDay`).

### `ChangeStatus`
```csharp
Status = newStatus;
CompletedAt = newStatus == TaskItemStatus.Completed ? now : null;
UpdatedAt = now;
```
`? :` er en betinget uttrykk: «hvis Completed, bruk `now`, ellers `null`». Én linje sørger for at fullføringstidspunktet settes ved fullføring og **fjernes** ved gjenåpning. UI-et skriver aldri til `CompletedAt` direkte.

---

## 5. Nullable-typer

`<Nullable>enable</Nullable>` står i `.csproj`-filene. Da er `string` «aldri null», og `string?` betyr «kan være null». Kompilatoren advarer deg hvis du bruker en `string?` uten å sjekke.

Eksempler:
- `public string? Description { get; private set; }`: beskrivelse er valgfri.
- `Task<HousingUnit?> GetByIdAsync(int id)`: returnerer `null` hvis enheten ikke finnes. Den som kaller må håndtere det, og `HousingUnitService.GetExistingUnitAsync` gjør det ved å kaste `KeyNotFoundException`.
- `DateOnly?` og `int?` er *Nullable value types*: `int?` kan være et tall eller `null`. `HousingUnitId` på en oppgave er `int?` fordi koblingen er valgfri.
- `bool?` i `EvCharger.HasElectricVehicle` har **tre** tilstander: ja, nei og «vet ikke». En vanlig `bool` ville tvunget «nei» som standard.

`??` (null-coalescing) gir en reserveverdi hvis venstre side er `null`: i `ChargingReconciler` står `subMeterKwh += line.Consumption.Kwh ?? 0m;`. Det betyr at en måler uten dokumentert forbruk bidrar med 0 til *delsummen*. Det er trygt fordi avstemmingen i tillegg merkes ufullstendig, og differansen er `null`, så delsummen aldri presenteres som et fullstendig tall.

---

## 6. Tjenester og Dependency Injection

`src/SameieHub.Domain/HousingUnitService.cs`:
```csharp
public class HousingUnitService
{
    private readonly IHousingUnitRepository _repository;

    public HousingUnitService(IHousingUnitRepository repository)
    {
        _repository = repository;
    }

    public async Task<HousingUnit> RegisterAsync(HousingUnitDetails details)
    {
        var unit = new HousingUnit(details);

        await _repository.AddAsync(unit);

        return unit;
    }
```
**Linje for linje i `RegisterAsync`:**
1. `new HousingUnit(details)` kjører konstruktøren. Er noe ugyldig, kastes `ArgumentException` her, og ingenting lagres.
2. `_repository.AddAsync(unit)` ber lagringslaget lagre. Tjenesten vet ikke om det er SQLite eller en fake. Når kallet er ferdig, har `unit.Id` fått verdi.
3. Objektet returneres, slik at siden kan sende brukeren til `/boenheter/{unit.Id}`.

**Hvorfor `private readonly IHousingUnitRepository _repository`?** `readonly` betyr at feltet bare kan settes i konstruktøren. Klassen kan altså ikke bytte repository underveis.

### Hvordan får tjenesten sitt repository? DI.
`src/SameieHub.Infrastructure/ServiceCollectionExtensions.cs`:
```csharp
services.AddDbContextFactory<SameieHubDbContext>(options =>
    options.UseSqlite($"Data Source={databasePath};Foreign Keys=True"));
services.AddScoped<IHousingUnitRepository, EfHousingUnitRepository>();
```
og i `src/SameieHub.Web/Program.cs`:
```csharp
builder.Services.AddScoped<HousingUnitService>();
builder.Services.AddScoped<TaskItemService>();
builder.Services.AddSingleton(TimeProvider.System);
```
Når en Razor-side skriver `@inject HousingUnitService HousingUnitService`, ser containeren at tjenesten trenger `IHousingUnitRepository`, finner registreringen (`EfHousingUnitRepository`), som trenger `IDbContextFactory<...>`, og bygger hele kjeden automatisk. Ingen steder i tjenestekoden står `new EfHousingUnitRepository(...)`.

I testene skriver vi i stedet `new HousingUnitService(new FakeHousingUnitRepository())`. Det er hele gevinsten: **samme tjenestekode, annen leverandør**.

### Tid som avhengighet: `TaskItemService`
```csharp
private DateTime GetUtcNow()
{
    return _timeProvider.GetUtcNow().UtcDateTime;
}
```
`TimeProvider` er en innebygd .NET-klasse. I produksjon er det `TimeProvider.System` (ekte klokke). I tester er det `ManualTimeProvider`. Alternativet `DateTime.UtcNow` direkte i koden ville gjort tidsstyrte regler vanskelige å teste.

---

## 7. `async`, `await`, `Task` og `Task<T>`

Databaseoperasjoner tar tid. Med `async/await` kan serveren gjøre annet arbeid mens den venter, i stedet for å blokkere en tråd.

- `Task` betyr «en operasjon som ferdigstilles senere, uten returverdi» (som `void`).
- `Task<HousingUnit?>` betyr «en operasjon som senere gir en `HousingUnit?`».
- `await` betyr «vent på at operasjonen blir ferdig, og fortsett her etterpå» (uten å blokkere tråden).
- Metoder som bruker `await` må merkes `async`. Konvensjon: navnet slutter på `Async`.

```csharp
public Task<HousingUnit?> GetByIdAsync(int id)
{
    return _repository.GetByIdAsync(id);
}
```
Her er `GetByIdAsync` **ikke** `async`. Den sender bare Task-en videre uten å gjøre noe selv etterpå, så `async/await` ville vært unødvendig ekstra arbeid. Det er derfor noen tjenestemetoder har `async` og andre ikke: `RegisterAsync` bruker `await` fordi den må gjøre noe *etter* lagringen (returnere objektet).

**En vanlig feil:** å glemme `await`. Da fortsetter koden før lagringen er ferdig, og unntak fanges ikke der du tror. Derfor er `Async`-metoder alltid `await`-et i koden vår.

---

## 8. EF Core: mapping og repository

### Mapping: `src/SameieHub.Infrastructure/Persistence/SameieHubDbContext.cs`
```csharp
private static void ConfigureTaskItem(ModelBuilder modelBuilder)
{
    var task = modelBuilder.Entity<TaskItem>();

    task.HasKey(entity => entity.Id);
    task.Property(entity => entity.Title).IsRequired();
    task.Property(entity => entity.Status).HasConversion<string>();
    task.Property(entity => entity.CreatedAt).HasConversion(UtcDateTimeConverter);
    // …
    task.HasOne<HousingUnit>()
        .WithMany()
        .HasForeignKey(entity => entity.HousingUnitId)
        .OnDelete(DeleteBehavior.Restrict);
}
```
- `Entity<TaskItem>()` velger hvilken klasse som beskrives. `HasKey` sier at `Id` er primærnøkkel (EF lager auto-økende).
- `IsRequired()` gir `NOT NULL` i databasen.
- `HasConversion<string>()` lagrer enum som tekst.
- `HasOne<HousingUnit>().WithMany().HasForeignKey(...)` sier at en oppgave peker på én boenhet via `HousingUnitId`. `WithMany()` uten argument betyr at **boenheten ikke har en liste over oppgaver** i koden sin. Det var et bevisst valg: Domain-klassene skal ikke kjenne hverandre i sirkel, og lister kan hentes via tjenestene.
- `DeleteBehavior.Restrict` sier at databasen skal nekte sletting av en boenhet som oppgaver peker på.

`UtcDateTimeConverter`:
```csharp
private static readonly ValueConverter<DateTime, DateTime> UtcDateTimeConverter =
    new(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
```
To lambdauttrykk: «hva som skal lagres» (verdien uendret) og «hva som skal gjøres når vi leser» (merk som UTC). Det trengs fordi SQLite ikke lagrer tidssone.

### Repository: `src/SameieHub.Infrastructure/Persistence/EfHousingUnitRepository.cs`
```csharp
public async Task<HousingUnit?> GetByIdAsync(int id)
{
    await using var context = await _contextFactory.CreateDbContextAsync();

    return await context.HousingUnits
        .AsNoTracking()
        .FirstOrDefaultAsync(unit => unit.Id == id);
}
```
**Linje for linje:**
1. `await using var context = ...` lager en ny, kortlivet `DbContext` og sørger for at den **disponeres** (frigjøres) når metoden er ferdig, også ved feil.
2. `context.HousingUnits` er tabellen. `AsNoTracking()` sier at EF ikke skal følge etter objektet for å oppdage endringer. Vi trenger ikke det: vi leser bare, og lagrer via `UpdateAsync` senere. Det gir mindre minnebruk og unngår at objektet «tilhører» en kontekst som snart er borte.
3. `FirstOrDefaultAsync(unit => unit.Id == id)` oversettes til SQL (`SELECT … WHERE Id = @id LIMIT 1`) og gir `null` hvis ingenting finnes. Uttrykket `unit => unit.Id == id` er et *lambda*: en liten navnløs funksjon.

```csharp
public async Task UpdateAsync(HousingUnit unit)
{
    await using var context = await _contextFactory.CreateDbContextAsync();

    // The unit was loaded by another context, so Update tells this one to save all its values.
    context.HousingUnits.Update(unit);
    await context.SaveChangesAsync();
}
```
Objektet kom fra en *annen* (nå borte) kontekst. `Update` forteller den nye konteksten «lagre alle verdiene til dette objektet». Alternativet var å hente raden på nytt og kopiere over feltene, som er mer kode.

`SaveChangesAsync()` er øyeblikket SQL faktisk kjøres. Alt før er bare forberedelse.

---

## 9. Exception-håndtering

Retningslinjen (CLAUDE.md): `try/catch` bare der feilen kan håndteres, presise unntakstyper, ingen tomme `catch`.

### Oversette en teknisk feil til en domenefeil: `EfHousingUnitRepository.DeleteAsync`
```csharp
try
{
    await context.SaveChangesAsync();
}
catch (DbUpdateException exception) when (SqliteConstraintErrors.IsForeignKeyViolation(exception))
{
    throw new HousingUnitInUseException(id, exception);
}
```
- `catch (…) when (betingelse)` er et **unntaksfilter**: fanger bare hvis betingelsen er sann. Andre `DbUpdateException` (for eksempel full disk) slippes gjennom uendret.
- Den nye `HousingUnitInUseException` får den opprinnelige feilen som `exception` (indre unntak), slik at ingenting går tapt ved feilsøking.
- Hvorfor oversette? Domain og Web skal ikke trenge å kjenne SQLite. De kjenner bare `HousingUnitInUseException`.

### Kjenne igjen feilen: `SqliteConstraintErrors`
```csharp
return sqliteException.SqliteExtendedErrorCode == ForeignKeyRestrictExtendedCode
    && sqliteException.Message.Contains("FOREIGN KEY", StringComparison.OrdinalIgnoreCase);
```
Koden 1811 deles av *alle* triggere i SQLite. Derfor sjekkes meldingen også, slik at en vanlig trigger ikke feiltolkes som «i bruk». Det ble funnet ved en test (en egen trigger i `TaskItemRelationTests`) og rettet.

### Fra feil til melding: `TaskForm.SaveAsync` (forkortet)
```csharp
try
{
    await OnSave(_model.ToDetails());
}
catch (ArgumentException exception) when (TaskFieldMessages.TryGet(exception.ParamName, out _))
{
    ShowFieldError(exception.ParamName!);
}
catch (KeyNotFoundException)
{
    _errorMessage = "Oppgaven finnes ikke lenger. Gå tilbake til oversikten.";
}
catch (Exception exception)
{
    // Only the exception is logged, never the form values.
    Logger.LogError(exception, "Could not save task.");
    _errorMessage = "Kunne ikke lagre. Kontroller at databasen er tilgjengelig og prøv på nytt.";
}
```
Rekkefølgen er viktig: de **mest spesifikke** `catch`-blokkene først, den generelle `Exception` sist. `out _` betyr «jeg trenger ikke verdien, bare svaret ja/nei». Siste blokk logger bare unntaket, **ikke** det brukeren skrev (kan inneholde personopplysninger).

---

## 10. LINQ

LINQ er C# sin måte å spørre i lister på (`Where`, `OrderBy`, `Count`, `Any`). CLAUDE.md ber om enkel kode, med `foreach` fremfor kompleks LINQ, så LINQ brukes i korte, lesbare uttrykk. To eksempler:

### `src/SameieHub.Domain/TaskItemFilter.cs`: `foreach` i stedet for LINQ
```csharp
foreach (var task in tasks)
{
    if (MatchesFilters(task, criteria) && MatchesText(task, searchText))
    {
        result.Add(task);
    }
}
```
Dette kunne vært `tasks.Where(...).ToList()`. `foreach` valgt fordi hvert steg er lett å lese og å sette en stopp-peker i for en nybegynner.

### `src/SameieHub.Domain/TaskItemSorting.cs`: sortering med flere nøkler
```csharp
_ => tasks
    .OrderBy(task => task.Status == TaskItemStatus.Completed)
    .ThenBy(task => task.DueDate == null)
    .ThenBy(task => task.DueDate)
    .ThenByDescending(task => task.CreatedAt)
    .ThenBy(task => task.Id)
```
Les den slik: sorter først slik at *ikke fullførte* kommer før fullførte (`false` sorteres før `true`), deretter de **med** frist før de uten, deretter etter frist (eldst først, så forfalte havner øverst), deretter nyeste opprettet, og til slutt `Id` slik at rekkefølgen alltid er den samme. Siste ledd kalles *tie-breaker*, og uten den kan to like oppgaver bytte plass mellom to visninger.

**`.OrderBy` på boolske uttrykk** er et vanlig triks: `false` kommer før `true`.

### I databasen: EF oversetter LINQ til SQL
`context.HousingUnits.AsNoTracking().OrderBy(unit => unit.Id).ToListAsync()` blir `SELECT … FROM HousingUnits ORDER BY Id`. Merk at `ToListAsync()` er det som **kjører** spørringen.

---

## 11. Test doubles og tester

### Fake repository: `tests/SameieHub.Tests/Fakes/FakeHousingUnitRepository.cs`
```csharp
public class FakeHousingUnitRepository : IHousingUnitRepository
{
    private readonly Dictionary<int, HousingUnit> _units = [];
    private int _nextId = 1;

    public Task AddAsync(HousingUnit unit)
    {
        unit.AssignId(_nextId);
        _units[unit.Id] = unit;
        _nextId++;

        return Task.CompletedTask;
    }
```
- Samme kontrakt som den ekte, men data ligger i en `Dictionary<int, HousingUnit>` i minnet.
- `[]` er en *collection expression* (tom dictionary).
- `Task.CompletedTask` er en ferdig `Task`: metoden er egentlig synkron, men må likevel levere en `Task` for å oppfylle grensesnittet.
- Merk at fakeen ikke forsøker å etterligne SQL. Den etterligner bare *kontrakten*. Det er derfor vi **også** har integrasjonstester mot ekte SQLite for alt som avhenger av databasen (fremmednøkler, migreringer).

Andre fakes gjør mer: `FakeBoardCaseRepository` har `IsFailing` (simulerer at databasen er nede) og `IsDeletionBlocked`. `ManualTimeProvider` er en klokke som bare flytter seg når testen kaller `Advance`.

### En enhetstest: `tests/SameieHub.Tests/Domain/TaskItemTests.cs`
```csharp
[Fact]
public void UpdateDetails_InvalidValue_DoesNotChangeUpdatedAt()
{
    var task = NewTask();

    Assert.Throws<ArgumentException>(() => task.UpdateDetails(FullDetails() with { Title = " " }, Later));

    Assert.Equal(CreatedAt, task.UpdatedAt);
}
```
Strukturen er **Arrange–Act–Assert**: lag objektet, utfør handlingen, sjekk resultatet. `[Fact]` markerer en test. `Assert.Throws<ArgumentException>(() => …)` forventer at lambdaen kaster akkurat den feilen. Testnavnet følger `Metode_Scenario_ForventetResultat`, så en rød test forteller hva som er galt uten å åpne den. `[Theory]` med `[InlineData(...)]` kjører samme test med flere verdier.

### En integrasjonstest-hjelper: `SqliteTestDatabase`
`SqliteTestDatabase.CreateAsync()` lager en temp-fil, kjører de ekte migreringene og gir tilgang til tjenester koblet til den. `RestartAsync()` kaster alle tjenester og bygger nye mot **samme fil** («omstart av appen»), og `MigrateToMigrationAsync("AddBoardCaseDecisionDate")` oppgraderer til en bestemt eldre versjon, slik at oppgraderingstester kan sette inn data før siste migrering kjøres.

### En bUnit-test
Komponenter rendres i minnet: `var cut = Render<TaskList>();` gir en «component under test», og `cut.Find("tbody tr")` søker i HTML-en. `UiTestBase` registrerer fakes og en `ManualTimeProvider` i DI.

---

## 12. Et Razor-skjema

`src/SameieHub.Web/Components/Pages/TaskItems/TaskForm.razor` (utdrag):
```razor
<EditForm EditContext="_editContext" OnValidSubmit="SaveAsync" novalidate>
    <DataAnnotationsValidator />
    …
    <InputText id="title" class="form-control" @bind-Value="_model.Title"
               maxlength="@TaskItem.MaxTitleLength" aria-required="true" />
    <ValidationMessage For="() => _model.Title" />
```
- `.razor` blander HTML og C#. `@bind-Value="_model.Title"` kobler tekstfeltet til egenskapen i begge retninger.
- `maxlength="@TaskItem.MaxTitleLength"` bruker **konstanten fra Domain**, så grensen ikke dupliseres i HTML-en.
- `_model` er en `TaskFormModel` (`Components/Pages/TaskItems/TaskFormModel.cs`) med `[Required(ErrorMessage = "Tittelen må fylles ut.")]`. Den inneholder bare *skjemaets* verdier.
- `ToDetails()` i `TaskFormModel` gjør dem om til `TaskItemDetails` før kallet til tjenesten, så Domain ikke trenger å kjenne skjemaklassen.
- `OnValidSubmit="SaveAsync"`: kalles bare hvis `[Required]`-reglene er oppfylt.
- `_isSaving` i `SaveAsync` hindrer dobbel innsending (knappen deaktiveres, og metoden returnerer tidlig hvis den allerede kjører).

**Hvorfor en egen `…FormModel` og ikke binde direkte til `TaskItemDetails`?** En `record` med `init` kan ikke endres etter opprettelse, men skjemaet må kunne endre verdiene mens brukeren skriver. Alternativet er å la skjemaet lage en ny record ved hver endring, som er mer tungvint.

---

## Hvor bør du lese videre?

- Vil du se et helt forløp: følg `Registrer oppgave` i sekvensdiagrammet i [ARCHITECTURE_EXPLAINED.md](ARCHITECTURE_EXPLAINED.md), og åpne filene i denne rekkefølgen: `TaskForm.razor` → `TaskCreate.razor` → `TaskItemService.cs` → `TaskItem.cs` → `EfTaskItemRepository.cs` → `SameieHubDbContext.cs`.
- Vil du se elbilladeregnestykket: `MeterHistory.cs` → `ChargingReconciliation.cs` → `ChargingService.cs` (domenelaget) og `EfChargingRepositories.cs` for transaksjonene (lagring, M4E-B).
- Vil du se hvorfor: [DESIGN_DECISIONS.md](DESIGN_DECISIONS.md).
