# Møtemodulen: referanse til eksisterende kode

En oversikt over filene i SameieHub som studenten og veilederen skal bruke som forbilder. Alt er lest fra koden slik den er på commit `eb1e1f1`. Stiene er relative til rotmappen. **Ingen møtekode finnes ennå.**

Beste hovedforbilde: **Styresaker (`BoardCase`)** for en enkel entitet med status og regler. **Oppgaver (`TaskItem`)** for koblinger til flere andre ting. **Elbillading** for flere entiteter som må lagres samlet.

Les også: [MEETING_MODULE_ARCHITECTURE.md](MEETING_MODULE_ARCHITECTURE.md) (hvordan delene henger sammen).

## 1. Domene (`src/SameieHub.Domain`)

Ingen EF Core, SQLite eller Blazor her (testet av `DependencyDirectionTests`).

| Fil | Type | Viktige medlemmer | Hva den gjør og hvordan den brukes | Hvorfor relevant |
|---|---|---|---|---|
| `src/SameieHub.Domain/BoardCase.cs` | `class BoardCase` | konstanter (`MaxTitleLength`, `MinMeetingDate` …), privat konstruktør for EF, `BoardCase(BoardCaseDetails, DateTime createdAt)`, `AssignId`, `UpdateDetails`, `ChangeStatus`, `RegisterDecision`, `ClearDecision`, `ToDetails` | Entiteten med alle regler: validering først, tilordning sist, `UpdatedAt` bare ved faktisk endring | **Hovedforbildet** for en `Meeting`-entitet. Inneholder også dagens `MeetingDate`, `Decision`, `DecisionDate` (se åpne spørsmål) |
| `src/SameieHub.Domain/BoardCaseDetails.cs` | `record BoardCaseDetails` | `required string Title`, `init`-egenskaper | Det brukeren kan redigere, som en uforanderlig «pakke» | Mønsteret for `…Details` til nye entiteter. `with` brukes i `BoardCaseService.ChangeHousingUnitAsync` |
| `src/SameieHub.Domain/BoardCaseService.cs` | `class BoardCaseService` | `RegisterAsync`, `GetByIdAsync`, `GetAllAsync`, `UpdateDetailsAsync`, `ChangeStatusAsync`, `RegisterDecisionAsync`, `ClearDecisionAsync`, `DeleteAsync` | Bruksområdene: henter, sjekker regler som involverer **andre** entiteter, kaller entiteten, lagrer. Bruker `TimeProvider` | Forbildet for `MeetingService` |
| `src/SameieHub.Domain/IBoardCaseRepository.cs` | `interface` | `AddAsync`, `GetByIdAsync`, `GetAllAsync`, `UpdateAsync`, `DeleteAsync` (returnerer `false` hvis ikke funnet) | Kontrakten domenet trenger; implementeres i Infrastructure | Mønsteret for repository-interface |
| `src/SameieHub.Domain/BoardCaseInUseException.cs` | `class : Exception` | konstruktør med id og indre unntak | Sletting nektes fordi noe peker på saken | Forbildet for en `MeetingInUseException` hvis noe kan peke på møter |
| `src/SameieHub.Domain/BoardCaseStatus.cs`, `BoardCasePriority.cs` | `enum` | `New, InProgress, Decided, Closed` | Lagres som tekst i databasen | Møtestatus blir trolig en `enum` på samme måte |
| `src/SameieHub.Domain/BoardCaseFilter.cs` | `record BoardCaseFilterCriteria`, `static class BoardCaseFilter` | `Apply` | Filtrering i minnet, uten database | Møtelisten kan filtreres likt |
| `src/SameieHub.Domain/BoardCaseSorting.cs` | `enum BoardCaseSortOrder`, `static class BoardCaseSorting` | `Sort` | Stabil sortering med norsk tekstsammenligning | Sortering av møter og saksliste |
| `src/SameieHub.Domain/TaskItem.cs` | `class TaskItem` | `UpdateDetails`, `ChangeStatus` (setter/nuller `CompletedAt`), `IsOverdue` | Entitet med koblingene `HousingUnitId`, `BoardCaseId` (bare id-er, ingen navigasjonsegenskaper) | Forbilde for koblinger og for oppfølgingsoppgaver |
| `src/SameieHub.Domain/TaskItemDetails.cs` | `record` | `BoardCaseId`, `HousingUnitId`, `DueDate`, `AssignedTo` | Redigerbare opplysninger | En oppfølgingsoppgave fra et møte lages med denne |
| `src/SameieHub.Domain/TaskItemService.cs` | `class TaskItemService` | `RegisterAsync` (sjekker at koblinger finnes), `ChangeBoardCaseAsync`, `GetToday`, `GetOverdueAsync` | Regler for koblinger: ny kobling sjekkes, uendret beholdes | Samme regel trengs når møter kobles til styresaker |
| `src/SameieHub.Domain/ChargingRepositories.cs` | flere `interface` | f.eks. `AddWithMainMeterAsync(facility, createMainMeter)` | Lagrer to ting i **en** transaksjon | Møte + agendapunkter må eventuelt lagres samlet |
| `src/SameieHub.Domain/TextRules.cs` | `internal static class TextRules` | `NormalizeOptional`, `NormalizeRequired`, `EnsureUtc`, `EnsurePositiveId` | Delt validering (brukt av elbillading) | Kan gjenbrukes av en ny entitet i stedet for å kopiere hjelpemetodene i `BoardCase` |
| `src/SameieHub.Domain/NorwegianTime.cs` | `static class NorwegianTime`, `enum LocalTimeProblem` | `FromUtc`, `DateOf`, `TryConvertToUtc` | Norsk tid og sommertid | Møter har klokkeslett: les dette før dere velger tidstype |
| `src/SameieHub.Domain/DashboardOverview.cs` | `class DashboardOverview`, `record UpcomingDate`, `enum UpcomingDateKind { …, BoardMeeting }` | `Create`, `FindUpcomingDates` | Bygger oversikten. «BoardMeeting» kommer fra `BoardCase.MeetingDate` | Må avklares når møter finnes |
| `src/SameieHub.Domain/HousingUnit.cs`, `HousingUnitService.cs` | `class` | `AssignId`, `DeactivateAsync`, `DeleteAsync` | Boenheten og «inaktiv i stedet for slettet» | Deltakere/eiere hører til boenheter (personopplysninger) |
| `src/SameieHub.Domain/ChargingService.cs` | `class ChargingService` | mange | Eksempel på tjeneste med flere repositories og samlede operasjoner | Større mønster, les ved behov |

## 2. Infrastruktur (`src/SameieHub.Infrastructure`)

| Fil | Type og metoder | Hva og hvordan | Hvorfor relevant |
|---|---|---|---|
| `src/SameieHub.Infrastructure/ServiceCollectionExtensions.cs` | `AddSameieHubInfrastructure(services, databasePath)`, `MigrateSameieHubDatabaseAsync` | Registrerer `DbContextFactory` (`Foreign Keys=True`), repositories (`AddScoped`), backup, restore, oppgraderer. Oppretter/migrerer ved oppstart | **Nytt repository registreres her** |
| `src/SameieHub.Infrastructure/Persistence/SameieHubDbContext.cs` | `DbSet<…>`, `OnModelCreating`, `ConfigureBoardCase`, `ConfigureTaskItem`, `ConfigureCharging`, `UtcDateTimeConverter` | Tabeller, `enum`→tekst, UTC-konvertering, fremmednøkler med `Restrict`, indekser og CHECK | **Her legges nye tabeller til** |
| `src/SameieHub.Infrastructure/Persistence/EfBoardCaseRepository.cs` | `AddAsync`, `GetByIdAsync` (`AsNoTracking`), `GetAllAsync`, `UpdateAsync`, `DeleteAsync` | Egen kortlevd `DbContext` per metode; `Update` lagrer alle verdier; fremmednøkkelfeil blir `BoardCaseInUseException` | **Hovedforbildet** for `EfMeetingRepository` |
| `src/SameieHub.Infrastructure/Persistence/EfTaskItemRepository.cs` | samme mønster | | Andre forbilde |
| `src/SameieHub.Infrastructure/Persistence/EfChargingRepositories.cs` | `AddWithMainMeterAsync`, `AddWithSubMeterAsync`, `AddRangeAsync` (`BeginTransactionAsync`) | Transaksjoner og flere rader samtidig | Hvis møte og punkter må lagres atomisk |
| `src/SameieHub.Infrastructure/Persistence/SqliteConstraintErrors.cs` | `IsForeignKeyViolation`, `IsUniqueViolation`, `IsCheckViolation` | Tolker SQLite-feilkoder | Ved slette- og unikhetsregler |
| `src/SameieHub.Infrastructure/Persistence/Migrations/` | `20261117121842_InitialCreate` … `20261120091703_AddChargingFacility`, `SameieHubDbContextModelSnapshot.cs` | Eneste måten skjemaet lages på | Les `20261118120607_AddBoardCases.cs` som mal; **endre aldri en anvendt migrering** |
| `src/SameieHub.Infrastructure/Persistence/DesignTimeDbContextFactory.cs` | `CreateDbContext` | Brukes bare av `dotnet ef`; peker på `design-time.db` (en kastefil) | Derfor bruker vi ikke `dotnet ef database update` |
| `src/SameieHub.Infrastructure/Persistence/DatabaseLocation.cs` | `GetDefaultPath` | Standard databasebane | |
| `src/SameieHub.Infrastructure/Persistence/DatabaseUpgrader.cs` | `UpgradeAsync` | Windows-versjonen tar kopi før migrering og nekter nyere database | Nye migreringer påvirker det automatisk |
| `src/SameieHub.Infrastructure/Backup/DatabaseBackupValidator.cs` | `FindProblems`, `FindLastMigrationId` | Krever **alle tabeller i modellen** og alle migreringer | En eldre sikkerhetskopi blir «Older» og oppgraderes ved gjenoppretting; test det |
| `src/SameieHub.Infrastructure/Backup/BackupContentSummary.cs` | `record BackupContentSummary(HousingUnits, BoardCases, Tasks, Chargers, MeterReadings)`, `Read` | Tellingene på sikkerhetskopisiden | **Vurder om møter skal med** |
| `src/SameieHub.Infrastructure/Restore/BackupFileInspector.cs`, `SqliteDatabaseRestore.cs` | `Inspect`, `PrepareAsync`, `ApplyPendingRestore` | Klassifiserer og gjenoppretter | Ikke endre uten god grunn |
| `src/SameieHub.Infrastructure/Demo/DemoDatabaseSeeder.cs` | `SeedAsync`, `InspectAsync`, `AddCaseAsync` | Fiktiv demo («Fiktivdalen Sameie») | Møter kan senere inn i demoen (eget valg) |

## 3. Web (`src/SameieHub.Web`)

### 3.1 Oppstart og registrering

| Fil | Relevant innhold |
|---|---|
| `src/SameieHub.Web/Program.cs` | Utviklingsstart og Windows-modus. Ved utviklingsstart: eventuell gjenoppretting, så `MigrateSameieHubDatabaseAsync`, så pipeline. Logger `Using database <mappe>/<fil>`. |
| `src/SameieHub.Web/Hosting/WebAppSetup.cs` | `AddServices`: **tjenester registreres her** (`HousingUnitService`, `BoardCaseService`, `TaskItemService`, `ChargingService` som `Scoped`; `TimeProvider.System`). `UsePipeline`. |
| `src/SameieHub.Web/Components/App.razor` | Interaktiv serverrendering **uten prerender** (sidene tegnes først når Blazor-forbindelsen er oppe). |
| `src/SameieHub.Web/Components/Routes.razor`, `_Imports.razor` | Ruter og felles `@using` (bl.a. `SameieHub.Domain`, `Components.Shared`). |
| `src/SameieHub.Web/Components/Layout/NavMenu.razor` | Hovedmenyen (seks punkter i fast rekkefølge) og bunnfelt. Nytt menypunkt krever bevisst beslutning og testendring. |

### 3.2 Sider for styresaker (forbildet for møtesidene)

Mappen `src/SameieHub.Web/Components/Pages/BoardCases/`:

| Fil | Innhold | Viktige deler |
|---|---|---|
| `BoardCaseList.razor` | `/styresaker` | `LoadAsync`, `QuickTabs`, søk, `Disclosure` for filtre, tom-tilstand, tabell |
| `BoardCaseCreate.razor` | `/styresaker/ny` | `[SupplyParameterFromQuery]` forhåndsvalg, `RegisterAsync`, navigerer til `?status=opprettet` |
| `BoardCaseDetail.razor` | `/styresaker/{Id:int}` | `PageHeader` med hovedhandling og «Flere handlinger», `RunActionAsync` (vern mot dobbeltklikk og feilmeldinger), slettebekreftelse |
| `BoardCaseEdit.razor` | `/styresaker/{Id}/rediger` | gjenbruker skjemaet |
| `BoardCaseForm.razor` | felles skjema | `EditForm`, `EditContext`, `ValidationMessageStore`, `OnSave`-delegat, `SaveAsync` med `catch (ArgumentException)` → feltfeil |
| `BoardCaseFormModel.cs` | `From`, `ToDetails`, `[Required]` | Kun tittel valideres i UI; alt annet i domenet |
| `BoardCaseFieldMessages.cs` | `TryGet(paramName)` | Norske meldinger per `ArgumentException.ParamName` |
| `BoardCaseText.cs` | `StatusText`, `PriorityText`, `AllStatuses` | Norske navn på `enum`-verdier (koden er engelsk) |
| `BoardCaseStatusBadge.razor`, `BoardCasePriorityBadge.razor` | merker | Status med tekst, ikke bare farge |

Oppgaver (`Pages/TaskItems/`): `TaskCreate.razor` (forhåndsvalg `?styresak=` og `?boenhet=`, viktig for oppfølgingsoppgaver fra møter), `TaskDueDate.razor`.
Forsiden: `Pages/Dashboard.razor` (teksten «SameieHub holder ikke styr på selve møtene» og «Kommende saker med møtedato»).

### 3.3 Delte komponenter (`Components/Shared/`)

| Komponent | Parametre |
|---|---|
| `PageHeader.razor` | `Title`, `Description`, `BackHref`, `BackText`, `ChildContent`, `PrimaryAction`, `MoreActions` |
| `ActionFeedback.razor` | `Message`, `Kind` (`FeedbackKind`), `ActionText`, `OnAction`, `OnDismiss` |
| `Disclosure.razor` | `Label`, `Hint`, `Open`, `ForceOpen`, `ChildContent` |
| `QuickTabs.razor` | `AriaLabel`, `Tabs` (`QuickTab(Key, Text, Count, Href)`), `Selected`, `SelectedChanged` |
| `ActiveFilters.razor` | `Filters`, `OnRemove`, `OnClearAll` |
| `DetailItem.razor` | `Label`, `Value`, `LinkPrefix` |
| `MoreMenu.razor` | `Label`, `ButtonClass`, `ChildContent` |
| `ScopeField.razor` | «Hele sameiet / en boenhet»: `UnitMode`, `HousingUnitId`, `Units`, `For`, `Help`, `Error` |
| `NorwegianDateFormat.cs` | `Date(DateOnly)`, `DateTime(DateTime utc)` |
| `UiTypes.cs` | `ActiveFilter`, `QuickTab`, `FeedbackKind` |

Stiler: `wwwroot/app.css` (variabler `--sh-…`, `.surface-card`, `.form-section`, `.form-actions`, `.status-badge`, `table-cards` for smal skjerm) og Bootstrap 5 lokalt.

## 4. Tester (`tests/SameieHub.Tests`)

Totalt 1733 tester (xUnit 2.9.3, bUnit 2.15.2, `Microsoft.AspNetCore.Mvc.Testing`).

| Fil | Hva den viser | Bruk som forbilde for |
|---|---|---|
| `Domain/BoardCaseTests.cs` | Entitetsregler: standardverdier, ugyldige verdier, UTC, trimming | Tester av `Meeting` |
| `Domain/BoardCaseServiceTests.cs` | Tjeneste med fake repositories og `ManualTimeProvider` | Tester av `MeetingService` |
| `Domain/BoardCaseFilterAndSortingTests.cs` | Rene funksjoner | Filtrering/sortering |
| `Fakes/FakeBoardCaseRepository.cs` | `IsFailing`, `IsDeletionBlocked`, `AssignId` | Lag en tilsvarende fake for møter |
| `Fakes/ManualTimeProvider.cs` | `Advance` | Alle tider |
| `Infrastructure/SqliteTestDatabase.cs` | ekte SQLite i en midlertidig mappe; `CreateAsync`, `CreateUnmigratedAsync`, `MigrateToMigrationAsync`, `ExecuteSqlAsync`, `RestartAsync`, `BuildProvider` | **Nye tjenester må registreres i `BuildProvider`** |
| `Infrastructure/BoardCaseMigrationTests.cs` | Migrering fra eldre skjema uten å ødelegge data | Migreringstester for møter |
| `Infrastructure/BoardCasePersistenceTests.cs`, `BoardCaseRelationTests.cs` | Persistens og fremmednøkler (`RESTRICT`) | Persistens-/relasjonstester |
| `Infrastructure/DatabaseTestData.cs`, `DatabaseTestFiles.cs` | `FillAllModulesAsync`; `SnapshotAsync` (en linje per tabell) | **Legg nye tabeller til her** så backup-/restoretestene dekker dem |
| `Web/UiTestBase.cs` | bUnit-oppsett med fakes (`UseShutdown`, `CurrentPath`) | **Nye tjenester og fakes registreres her** |
| `Web/UiTestHelpers.cs` | `FindByTestId`, `FindFieldByLabel`, `ClickButton`, `ChooseHousingUnit` | Bruk i stedet for skjøre CSS-selektorer |
| `Web/BoardCaseCreateTests.cs`, `…DetailTests.cs`, `…EditTests.cs`, `…ListTests.cs` | Skjema, detalj og liste | UI-tester |
| `Web/NavigationConsistencyTests.cs` | **Hovedmenyen har nøyaktig de seks punktene og ordet «møte» finnes ikke i den** (`NavMenu_HasTheAgreedOrder…`) | Må endres bevisst hvis møter får menypunkt |
| `Web/DashboardTests.cs` | Teksten om at en dato ikke er et bekreftet møte (`Upcoming_ExplainsThatADateIsNotAConfirmedMeeting…`) | Må endres bevisst hvis dashbordet endres |
| `Web/BoardCaseDetailTests.cs`, `BoardCaseListTests.cs` | «Møtedato»-kolonnen og at «Neste styremøte» ikke vises | Samme |
| `Architecture/DependencyDirectionTests.cs` | Domain refererer ikke EF/Blazor | Skal fortsatt være grønn |

## 5. Verktøy og rutiner

| Fil | Innhold |
|---|---|
| `SameieHub.slnx` | Løsningen (fire prosjekter) |
| `dotnet-tools.json` | `dotnet-ef` 10.0.15 (`dotnet tool restore`) |
| `.editorconfig` | Kodestil (filscopede navnerom, krøllparenteser, 4 mellomrom) |
| `scripts/demo.ps1`, `scripts/Start-demo.cmd`, `scripts/Nullstill-demo.cmd` | Demodatabase (`%LOCALAPPDATA%\SameieHubDemo`, port 5100) |
| `.github/workflows/ci.yml` | `build-and-test`, `windows-package`, `windows-installer` |
| `.github/workflows/release-build.yml` | Bare manuelt eller på tag; publiserer ingenting |
| `docs/` | `ARCHITECTURE.md`, `UI_DESIGN.md`, `TESTING.md`, `DEVELOPMENT.md`, `CODE_WALKTHROUGH.md`, `DESIGN_DECISIONS.md`, `PROJECT_STRUCTURE.md`, `REQUIREMENTS.md` |
| `CLAUDE.md` | Prosjektregler (kodestil, TDD, git, læring) |
