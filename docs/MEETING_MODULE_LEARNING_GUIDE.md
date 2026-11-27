# Møtemodulen: læringsguide

Denne guiden forklarer det du må forstå for å skrive møtemodulen selv. Eksemplene er **fra koden i SameieHub**. Guiden implementerer ingenting. Den korte linje-for-linje-gjennomgangen finnes i [CODE_WALKTHROUGH.md](CODE_WALKTHROUGH.md); denne går grundigere inn på det du trenger for en ny modul.

## Slik lærer vi (avtale mellom student og ChatGPT)

Studenten skriver **all** produksjonskode. ChatGPT:

1. forklarer teorien når den trengs, kort og med eksempler fra denne koden,
2. stiller kontrollspørsmål **før** og etter,
3. gir små oppgaver (en test eller en metode om gangen),
4. gir **hint før løsning**: først en ledetråd, så et pseudokode-skisse, til slutt kode bare hvis studenten ber om det etter å ha prøvd,
5. gjennomgår studentens kode (regler, navn, feilhåndtering, tester),
6. hjelper med feilsøking ved å spørre hva du forventet og hva du så,
7. forklarer testresultater (hva rødt betyr, ikke bare hvordan det blir grønt).

Hold koden enkel og forståelig. Tydelige `if`/`else`, `foreach`, små metoder. Ikke ny arkitektur uten behov (`CLAUDE.md`).

**Før du starter en økt**, gi ChatGPT de fire `MEETING_MODULE_*`-dokumentene, de to-tre filene du jobber med, og testen eller feilmeldingen. Lim aldri inn databaser, sikkerhetskopier eller virkelige navn.

## TDD og Arrange, Act, Assert

TDD i dette prosjektet: **test først → se at den feiler av riktig grunn → skriv minst mulig kode → grønn → rydd opp**.

Hver test har tre deler. Fra `tests/SameieHub.Tests/Domain/BoardCaseServiceTests.cs`:

```csharp
[Fact]
public async Task RegisterAsync_InvalidTitle_ThrowsAndStoresNothing()
{
    // Arrange: skjer her i klassens konstruktør (fake repository, klokke, tjeneste)
    // Act + Assert: kallet skal kaste
    await Assert.ThrowsAsync<ArgumentException>(() => _service.RegisterAsync(Details("   ")));

    // Assert: og ingenting skal være lagret
    Assert.Empty(await _service.GetAllAsync());
}
```

- **Arrange:** lag det testen trenger. **Act:** gjør *en* ting. **Assert:** kontroller resultatet (og at ingen sideeffekter skjedde).
- Navnet er `Metode_Situasjon_ForventetResultat`.
- En test skal feile hvis regelen brytes. Prøv: kommenter ut regelen og se at testen blir rød.
- Kjør: `dotnet test --filter "FullyQualifiedName~BoardCaseServiceTests"`.

**Øvelse 1.** Les `BoardCaseTests.Constructor_MissingTitle_Throws`. Hvorfor er den en `[Theory]`? **Kontrollspørsmål:** Hva er forskjellen på «testen feiler» og «testen kompilerer ikke», og hvorfor skal du se den feile først?

## 1. Prosjektstruktur og lag

**Hva:** fire prosjekter (`Domain`, `Infrastructure`, `Web`, `Tests`). **Hvordan:** prosjektreferanser i `.csproj` bestemmer hva som får se hva. **Hvorfor:** reglene i `Domain` kan testes uten database eller nettleser, og lagringen kan byttes.

**Øvelse 2.** Åpne `src/SameieHub.Domain/SameieHub.Domain.csproj`. Hvorfor har den ingen pakkereferanser? **Spørsmål:** Hva ville gått galt hvis `BoardCase` brukte `DbContext`?

## 2. Klasser, egenskaper og entiteter

Fra `BoardCase.cs`:

```csharp
public string Title { get; private set; } = string.Empty;

private BoardCase()                      // for EF Core
{
}

public BoardCase(BoardCaseDetails details, DateTime createdAt)
```

- **`private set`:** bare entiteten selv kan endre verdien. Utenfra må du gå gjennom en metode som sjekker reglene.
- **Privat konstruktør:** EF Core trenger den for å bygge objektet fra databasen.
- **Offentlig konstruktør:** et gyldig objekt skal ikke kunne lages ugyldig.
- **`AssignId`:** repository (og fake) setter id én gang.

**Øvelse 3.** Hva skjer hvis `AssignId` kalles to ganger? Finn linjen. **Spørsmål:** Hvorfor er `Status` ikke med i `BoardCaseDetails`?

## 3. `record` og `with`

`BoardCaseDetails` er en `record` med `init`-egenskaper: verdiene settes ved opprettelse og endres ikke. I `BoardCaseService.ChangeHousingUnitAsync`:

```csharp
await SaveDetailsAsync(boardCase, boardCase.ToDetails() with { HousingUnitId = housingUnitId });
```

`with` lager en **kopi** med én verdi endret. **Hvorfor:** validering skjer likt uansett hvordan verdien endres.

**Øvelse 4.** Hva betyr `required` på `Title`? Hva skjer hvis du utelater den i en ny `BoardCaseDetails`? **Spørsmål:** Forskjellen på `init` og `set`?

## 4. Enums og regler for tilstand

`BoardCaseStatus { New, InProgress, Decided, Closed }`. `ChangeStatus` tillater alle bytter unntatt «Vedtatt uten vedtak». `Enum.IsDefined` fanger ugyldige tall.

**Hvorfor:** feil skal kunne rettes, bare det som gir feilaktige data nektes.

**Øvelse 5.** Tegn en tilstandsmaskin for `TaskItem.ChangeStatus` (hva skjer med `CompletedAt`?). **Spørsmål:** Hvilke statuser ville et møte trenge? (Dette er en åpen beslutning, ikke en kodeoppgave.)

## 5. Validering og feilhåndtering

Tre lag:

| Lag | Eksempel | Hva |
|---|---|---|
| Skjema | `[Required]` på `BoardCaseFormModel.Title` | rask tilbakemelding |
| Domene | `NormalizeTitle`, `ValidateMeetingDate` | **sannheten**; kaster `ArgumentException` med `ParamName` |
| Database | fremmednøkler, CHECK | siste vern |

Mønsteret i `UpdateDetails`: **valider alle verdier først, tilordne til slutt**, så objektet aldri blir halvveis endret.

```csharp
var title = NormalizeTitle(details.Title);
...                                 // alle valideringer
if (isUnchanged) { return; }
Title = title;
...
UpdatedAt = now;
```

`nameof(details.Notes)` gir feilmeldingen et feltnavn som UI oversetter (`BoardCaseFieldMessages`). Skriv aldri strengen for hånd.

**Unntakstyper:** `ArgumentException`/`ArgumentOutOfRangeException` (verdi), `InvalidOperationException` (feil tilstand), `KeyNotFoundException` (finnes ikke), `…InUseException` (slettevern). Fang bare unntak der du kan gjøre noe nyttig, logg unntaket uten brukerens verdier, aldri tom `catch`.

**Øvelse 6.** Finn alle `catch` i `BoardCaseForm.SaveAsync` og forklar rekkefølgen. Hvorfor ligger den generelle `catch (Exception)` sist? **Spørsmål:** Hvorfor testes regelen i domenet og ikke bare i skjemaet?

## 6. Nullable-typer

`int? HousingUnitId` betyr «kan mangle» og har en mening (`null` = hele sameiet). `string? Notes`. Kompilatoren advarer hvis du bruker en verdi uten å sjekke den.

**Øvelse 7.** Forklar `if (boardCase.MeetingDate is { } meetingDate && …)` i `DashboardOverview`. **Spørsmål:** Hvorfor `?` på `DateOnly? MeetingDate` og ikke en «tom» dato?

## 7. Datoer og tid

| Type | Brukes til |
|---|---|
| `DateOnly` | dato uten klokke (møtedato, frist) |
| `DateTime` (UTC, `DateTimeKind.Utc`) | tidspunkt noe skjedde (`CreatedAt`) |
| `TimeProvider` | klokken, injisert så tester kan styre den (`ManualTimeProvider`) |
| `NorwegianTime` | norsk dato og klokkeslett, sommertid |

**Hvorfor UTC:** lagring blir entydig, visning oversettes til norsk tid. **Klokkeslett på møter** må konverteres med `TryConvertToUtc` (en time finnes ikke om våren, en finnes to ganger om høsten). Se hvordan elbillading håndterer dette (`ReadingTimeField.razor`).

**Øvelse 8.** Hvorfor feiler `new BoardCase(details, DateTime.Now)`? **Spørsmål:** Hvorfor skal en test aldri bruke `DateTime.Now`?

## 8. LINQ og samlinger

`BoardCaseFilter` bruker `foreach` for filtrering (lett å lese); `BoardCaseSorting` bruker `OrderBy`/`ThenBy`. `IReadOnlyList<T>` returneres så mottakeren ikke endrer listen.

**Øvelse 9.** Forklar hvorfor `BoardCaseSortOrder.MeetingDate` sorterer på `MeetingDate == null` først. **Spørsmål:** Hvorfor er rekkefølgen «deterministisk» (`ThenBy(Id)`)?

## 9. `async` / `await`

Alt som venter på disk eller nett er `async` og returnerer `Task`. `await` lar tråden gjøre annet mens vi venter. Regel: `await` helt opp, ikke `.Result` eller `.Wait()`.

**Øvelse 10.** Hvorfor returnerer `GetByIdAsync` i tjenesten `Task<BoardCase?>` uten `async`? **Spørsmål:** Hva skjer med et unntak i en `async`-metode som ikke `await`-es?

## 10. Interfaces og Dependency Injection

`IBoardCaseRepository` er det tjenesten trenger; `EfBoardCaseRepository` oppfyller det. DI-containeren lager objektene.

```csharp
public BoardCaseService(IBoardCaseRepository boardCaseRepository, IHousingUnitRepository housingUnitRepository, TimeProvider timeProvider)
```

**Hvorfor:** testen kan gi en fake. Levetid: `AddScoped` (en per Blazor-forbindelse), `AddSingleton` (en for hele programmet). Registreres i `ServiceCollectionExtensions` (repositories) og `WebAppSetup` (tjenester).

**Øvelse 11.** Hva skjer ved oppstart hvis du glemmer å registrere et repository? (Prøv på en egen gren og les feilmeldingen.) **Spørsmål:** Hvorfor ikke `new EfBoardCaseRepository()` inne i tjenesten?

## 11. Repository med EF Core

`EfBoardCaseRepository`:

```csharp
await using var context = await _contextFactory.CreateDbContextAsync();
return await context.BoardCases.AsNoTracking().FirstOrDefaultAsync(boardCase => boardCase.Id == id);
```

- **Kortlevd `DbContext`** per operasjon, fordi en Blazor-forbindelse lever lenge.
- **`AsNoTracking`:** lesing uten sporing.
- **`Update` + `SaveChangesAsync`:** objektet ble lest av en annen kontekst, så alle verdier lagres.
- **Slett:** fremmednøkkelfeil oversettes til en domenefeil.

**Øvelse 12.** Hvorfor er `DeleteAsync` sin `try/catch` rundt bare `SaveChangesAsync`? **Spørsmål:** Hva er forskjellen på `Add`, `Update` og `Remove`?

## 12. DbContext, mapping og migreringer

I `SameieHubDbContext.ConfigureBoardCase`: nøkkel, `Title` påkrevd, `enum` → tekst, UTC-konverter, `HasOne<HousingUnit>()…OnDelete(DeleteBehavior.Restrict)`.

En **migrering** er en klasse med `Up` (endre skjemaet) og `Down`. Les `20261118120607_AddBoardCases.cs`. Snapshotfilen viser modellen slik EF tror den er.

Regler: aldri endre en migrering som er anvendt; kontroller generert kode; ikke-valgfrie kolonner i eksisterende tabeller må ha en standardverdi; test at data fra eldre skjema overlever (`BoardCaseMigrationTests`).

**Øvelse 13.** Les `AddBoardCases`: finn `onDelete` og indeksene. **Spørsmål:** Hvorfor `Restrict` og ikke `Cascade`?

## 13. Blazor-komponenter

- En `.razor`-fil har markup og `@code`. `@page "/rute"` gjør den til en side. `@inject` henter tjenester.
- **Parametre:** `[Parameter]` inn, `EventCallback` eller delegat ut (`OnSave`).
- **Livssyklus:** `OnInitializedAsync` (hent data), `OnParametersSetAsync` (parametre endret, f.eks. `Id`), `OnAfterRenderAsync` (fokus, JS).
- **Binding:** `@bind` og `InputText @bind-Value`. `EditForm` + `EditContext` + `ValidationMessageStore` gir validering.
- **Spørringsparametre:** `[SupplyParameterFromQuery(Name = "styresak")]` (se `TaskCreate.razor`).
- **Navigasjon:** `NavigationManager.NavigateTo`.
- **Gjenbruk:** se delte komponenter i [MEETING_MODULE_REFERENCE.md](MEETING_MODULE_REFERENCE.md).
- **Tilgjengelighet:** `label` for alle felt, `role="alert"`/`status`, knapper for handlinger og lenker for navigasjon.

**Øvelse 14.** Følg `BoardCaseCreate.RegisterAsync` fra knappen til navigasjonen. **Spørsmål:** Hvorfor har skjemaet `_isSaving`?

## 14. Testtypene

| Type | Hva | Eksempel |
|---|---|---|
| Enhetstest (domene) | regler uten database | `BoardCaseTests`, `BoardCaseServiceTests` med `FakeBoardCaseRepository` |
| Integrasjon (SQLite) | ekte database i midlertidig mappe | `BoardCasePersistenceTests`, `BoardCaseRelationTests`, `BoardCaseMigrationTests` |
| bUnit (UI) | tegner komponenter uten nettleser | `BoardCaseCreateTests` |
| Arkitektur | avhengighetsretning | `DependencyDirectionTests` |
| Manuell | nettleser, mot egen testdatabase | |

bUnit-eksempel:

```csharp
cut.Find("#title").Change("  Maling av fellesarealer  ");   // Arrange/Act
cut.Find("form").Submit();
var boardCase = Assert.Single(await BoardCases.GetAllAsync());   // Assert
cut.WaitForAssertion(() => Assert.Equal($"/styresaker/{boardCase.Id}?status=opprettet", CurrentPath));
```

Bruk `UiTestHelpers` (`FindByTestId`, `FindFieldByLabel`, `ClickButton`) så testene tåler at markupen endres.

**Øvelse 15.** Hvorfor har fake repository `IsFailing`? Finn en test som bruker den. **Spørsmål:** Hva kan en SQLite-test finne som en fake-test ikke kan? (Fremmednøkler, UTC-konvertering, migreringer.)

## 15. Kodestil og navn

Engelske navn i kode, norske tekster i UI. Filscopede navnerom, krøllparenteser, 4 mellomrom (`.editorconfig`). XML-kommentar bare når den sier noe signaturen ikke viser. Ingen TODO eller undervisningskommentarer i ferdig kode. Navn på filene følger klassen.

## Sjekk deg selv før milepæl 1

Kan du forklare, uten å se:

1. Hvilket prosjekt får kjenne til hvilke andre, og hvorfor?
2. Hvor står reglene: skjema, tjeneste, entitet eller database?
3. Hva skjer fra du trykker «Lagre» til raden ligger i SQLite?
4. Hvorfor tar `BoardCaseService` imot interfaces?
5. Hva er forskjellen på de tre testtypene?
6. Hvorfor er dato `DateOnly` og tidspunkt `DateTime` i UTC?
