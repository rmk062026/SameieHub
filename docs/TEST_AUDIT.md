# Testrevisjon (M4T)

Dette er en **revisjon**, ikke en opprydding. Ingen tester er slettet, skrevet om eller lagt til, og ingen applikasjonskode er endret. Dokumentet beskriver hva testpakken inneholder, hva den gir av trygghet, hvor den gjentar seg selv, hva som er skjørt, hva som mangler, og en rekkefølge for forbedring som du kan godkjenne i steg.

Målet er ikke færrest mulig tester, men en testpakke som er rask, forståelig og vedlikeholdbar, og som fanger de feilene som faktisk er farlige for et sameie: feil målerstander, tapte data, brutte relasjoner og feil datoer.

## 1. Kort konklusjon

1. **Testpakken er i hovedsak verdifull.** Mesteparten av de 1240 testene dekker egne regler, grenseverdier, dataintegritet eller feil som er funnet tidligere. Tallet 1240 er også høyere enn antall testmetoder: det er **1083 testmetoder** (1005 `[Fact]` og 78 `[Theory]`), og de 78 teoriene gir 235 `InlineData`-rader som hver teller som en egen test.
2. **Det reelle problemet er ikke totalantallet, men tre andre ting:**
   - **Samme regel testes ett sted per lag** (entitet, tjeneste, lagring, side). For `UpdatedAt` som ikke skal endres ved uendrede verdier finnes det tester i fire lag. Det gir lite ekstra trygghet og mye ekstra vedlikehold.
   - **Demotestene er trege.** De 92 demotestene bruker omtrent 27 av de ca. 45 sekundene testpakken bruker, fordi hver test oppretter en ny SQLite-fil og seeder hele demoen på nytt. Alle 640 domenetester kjører til sammen på 0,2 sekunder.
   - **Noen viktige brukerflyter og oppstartsfeil er ikke dekket**, særlig at sidene aldri testes mot ekte SQLite, og at `Program.cs` (DI, migrering, demokommandoer) ikke testes i det hele tatt. Feilen med `datetime-local` i M4E-C slapp gjennom 1200 tester og ble funnet i nettleser.
3. **Grov vurdering (et resultat av analysen, ikke et mål):** omtrent 100–150 tester (8–12 %) kan slås sammen til teorier eller fjernes uten tap av dekning. Den største praktiske gevinsten er likevel å gjøre demotestene raske, og å fylle de få hullene i punkt 8.
4. **Pakken er mer sammensatt enn nødvendig for et læringsprosjekt** på noen få punkter (lange testnavn, 800 linjer lange testfiler, en tung `UiTestBase` og to nesten like fake/ekte-lag). Kjernen, domenetestene, er enkel og lett å lese.

## 2. Metode og begrensninger

- Antall og fordeling er hentet fra `dotnet test --list-tests`, og tidene er målt per lag med `--filter`.
- Dupliseringen er funnet ved å lese **alle 1240 testnavn**, og ved å lese testkoden for de testene jeg skrev i M4E-A til M4E-D. Eldre tester (M1–M4) er vurdert ut fra testnavn og struktur.
- **Hver foreslått sammenslåing eller fjerning må derfor bekreftes ved å lese testkroppen før den gjennomføres** (se plan i punkt 11). Noen tester med like navn kan teste forskjellige detaljer.
- Jeg har ikke målt kodedekning. `coverlet.collector` er installert, men brukes ikke. Dekning er ingen fasit, men én måling gir et nyttig utgangspunkt (se plan).

## 3. Oversikt: antall og tid

### Per modul og lag (antall kjørte tester)

| Modul | Domain | Infrastructure | Web | Sum |
|---|---:|---:|---:|---:|
| Boenheter | 87 | 28 | 54 | 169 |
| Styresaker | 149 | 40 | 81 | 270 |
| Oppgaver | 154 | 51 | 104 | 309 |
| Dashboard | 20 | – | 24 | 44 |
| Elbillading | 208 | 60 | 49 | 317 |
| Demodata (seeder) | – | 92 | – | 92 |
| Felles (norsk tid, norsk datoformat, SQLite-feilkoder) | 21 | 12 | 4 | 37 |
| Arkitektur (avhengighetsretning) | – | – | – | 2 |
| **Sum** | **639** | **283** | **316** | **1240** (inkl. 2 arkitekturtester) |

(`DemoDatabaseSeederTests` har 54 og `DemoChargingSeedTests` 38 kjørte tester.)

### Per testtype

| Type | Hva | Antall | Tid |
|---|---|---:|---|
| Enhetstester | Domain: entiteter, regler, filtre, avstemming, med fake repositories | 639 | 0,2 s |
| Integrasjonstester | Infrastructure: ekte SQLite per test, migreringer, fremmednøkler, transaksjoner | 283 | ca. 42 s |
| UI-tester | Web (bUnit): sider mot fake repositories | 316 | ca. 3 s |
| Arkitektur | Avhengighetsretning mellom prosjektene | 2 | – |

### Hva tiden går til

| Gruppe | Tester | Tid |
|---|---:|---|
| Demo (`DemoDatabaseSeederTests`, `DemoChargingSeedTests`) | 92 | ca. 27 s |
| Elbilladelagring (`ChargingPersistenceTests`, `ChargingAtomicityTests`, `ChargingMigrationTests`) | 60 | ca. 10 s |
| Styresaker/oppgaver i Infrastructure og Domain | 368 | ca. 8 s |

Det er altså **SQLite-fil per test**, og særlig **seeding av hele demoen per test**, som koster tid. Det er ikke antall tester i seg selv.

## 4. Det testene beskytter

### Forretningsregler (viktigst, hører hjemme i Domain)

| Regel | Hovedtester |
|---|---|
| Målerstander er akkumulerte, kan ikke gå ned, ingen duplikat på samme tid | `MeterHistoryTests.EnsureFits_*`, `ChargingServiceTests.RegisterReading_*`, `ChargingReadingSetTests.RegisterReadingSet_*` |
| Forbruk er differansen mellom avlesninger, aldri summen av målerstander | `MeterHistoryTests.BuildIntervals_*`, `ChargingReconcilerTests.Reconcile_SumsConsumptionNotAccumulatedMeterValues` |
| Avstemming er fullstendig bare med hovedmåler og åtte undermålere som alle er dokumentert; manglende verdi er aldri 0 | `ChargingExpectedSubMetersTests`, `ChargingReconcilerTests.Reconcile_ASubMeterWithoutReadings_*` |
| Toleranse på 24 timer og «tilnærmet» | `MeterHistoryTests.FindNearest_*`, `ApproximateReadingsAndAtomicityTests.ConsumptionBetween_*` |
| Norsk tid, sommer-/vintertid, tid som ikke finnes eller forekommer to ganger | `NorwegianTimeTests` |
| Styresak kan ikke være «Vedtatt» uten vedtak; vedtaksdato | `BoardCaseTests.ChangeStatus_*`, `BoardCaseServiceTests.ChangeStatusAsync_*` |
| Fullføringstidspunkt følger status for oppgaver; forfalt/frist i dag | `TaskItemTests.ChangeStatus_*`, `TaskItemTests.IsOverdue_*`, `TaskItemServiceTests.GetOverdueAsync_*` |
| Koblinger: aktiv boenhet kreves ved ny kobling, men eksisterende kobling til deaktivert boenhet beholdes | `*ServiceTests.*UnitDeactivatedAfterLinking*` |
| Validering og trimming av tekst, grenseverdier | `*Tests.UpdateDetails_ValuesAtTheLimits_AreAccepted`, `*_TooLongText_*` |
| Dashbordets tall, rekkefølge og grenser | `DashboardOverviewTests` |
| Demodata: bare på demostien, aldri over egne data, delvis seed avvises | `DemoDatabaseSeederTests.IsDemoDatabasePath_*`, `Seed_*IsNotChanged`, `DemoChargingSeedTests.Seed_Older*` |

### Dataintegritet og transaksjoner (skal beholdes)

| Hva | Tester |
|---|---|
| Atomisk lagring av avlesningssett og lader+måler, rollback ved feil | `ChargingAtomicityTests` (11), `ApproximateReadingsAndAtomicityTests.RegisterReadingSet_WhenStoringFails_*` |
| Fremmednøkler (`RESTRICT`), blokkert sletting, ingen foreldreløse rader | `BoardCaseRelationTests`, `TaskItemRelationTests`, `ChargingPersistenceTests.*CannotBeDeletedByTheDatabase`, `HousingUnitDeletionTests` |
| `CHECK`- og unike begrensninger i databasen | `ChargingPersistenceTests.Database_Refuses*`, `*_OnlyOneCanExist_TheServiceRefusesAndSoDoesTheDatabase` |
| Migrering fra eldre skjema med eksisterende data, uten tap | `HousingUnitMigrationTests`, `BoardCaseMigrationTests`, `TaskItemMigrationTests`, `ChargingMigrationTests` |
| Eksakt lagring av `decimal` (kWh) og UTC-tider | `ChargingPersistenceTests.Kwh_*`, `*Persistence*Timestamps*` |
| Omstart: data finnes etter ny `DbContext` | `*_AfterRestart*` i alle persistensklasser |
| Mapping av SQLite-feilkoder til domenefeil | `SqliteConstraintErrorsTests` |
| Avhengighetsretning | `DependencyDirectionTests` |

### Tester som beskytter mot feil som faktisk er funnet

Disse skal behandles som regresjonstester og ikke røres uten svært god grunn:

- `ChargingPagesTests.ReadingSetCreate_TimeWithSecondsFromTheBrowser_IsAccepted`, `ChargingInput_*WithAndWithoutSeconds` (`datetime-local` leveres med sekunder).
- `ChargingExpectedSubMetersTests` (avstemming ble «fullstendig» med færre enn åtte undermålere).
- `ChargingAtomicityTests.EveryChargerHasExactlyOneSubMeter_AlsoAfterSeveralFailedAttempts` og `*AfterAFailedAttempt*`.
- `DemoDatabaseSeederTests.Seed_RightCountsButNotTheDemoContent_*`, `Seed_CompleteDemoWithOneTaskDeleted_*`, `DemoChargingSeedTests.Seed_RightNumberOfRowsButWrongContent_*` (demo regnet som komplett bare fordi antall rader stemte).
- `TaskItemRelationTests.DeleteHousingUnit_BlockedByACustomTrigger_IsNotReportedAsInUse` (feil klassifisering av databasefeil).
- `HousingUnitDetailsTests.UpdateDetails_InvalidValue_ErrorMessageDoesNotContainTheValue`, `DashboardTests.Render_DatabaseFailure_DoesNotLogTextFromTheData`, `TaskCreateTests.Submit_DatabaseFailure_DoesNotLogTheEnteredText` (personvern i logger).

## 5. Duplisering

Det er viktig å skille mellom **nødvendig variasjon** (samme regel, ulike grenseverdier, som `UpdateDetails_ValuesAtTheLimits_*` og `FindNearest_ToleranceLimitIsIncluded`) og **meningsløs duplisering** (samme oppførsel i samme lag, eller samme regel bevist på nytt i hvert lag). Bare det siste er foreslått endret.

### 5.1 Samme regel i flere lag

| Regel | Hvor den testes | Vurdering |
|---|---|---|
| «Uendrede verdier endrer ikke `UpdatedAt`» (boenhet, styresak, oppgave, lader) | `BoardCaseTests.UpdateDetails_SameValues_DoesNotChangeUpdatedAt` og `…SameValuesWithExtraSpaces…`; `BoardCaseServiceTests.UpdateDetailsAsync_UnchangedValues_DoNotChangeUpdatedAt`; `BoardCasePersistenceTests.UpdateDetails_NothingChanged_KeepsStoredUpdatedAt`; `BoardCaseDetailTests.DecisionDate_SaveWithoutChanges_KeepsUpdatedAt`. Samme mønster for `TaskItemTests`/`TaskItemServiceTests`/`TaskItemPersistenceTests` og `EvChargerTests.UpdateDetails_NothingChanged_KeepsUpdatedAt`. | Regelen eies av entiteten. Behold entitetstesten. Tjenesten trenger **én** test som viser at tjenesten ikke skriver `UpdatedAt` selv, og lagringen trenger ingen egen test (ingen ny mapping). |
| Statusregler for styresak (Vedtatt krever vedtak, gjenåpne, samme status) | `BoardCaseTests.ChangeStatus_*` (14), `BoardCaseServiceTests.ChangeStatusAsync_*` (7), `BoardCaseDetailTests.ChangeStatus_*` (3) | Tjeneste og side trenger bare å vise at regelen slår gjennom (én feilsti og én vellykket sti). De fulle kombinasjonene hører hjemme i entiteten. |
| Vedtak registrert/korrigert/fjernet | `BoardCaseTests.RegisterDecision_*` (15) og `ClearDecision_*` (9), `BoardCaseServiceTests.RegisterDecisionAsync_*` (7) og `ClearDecisionAsync_*` (4), `BoardCaseDetailTests.Decision_*`/`DecisionDate_*` (17), `BoardCasePersistenceTests.Decision*` (4) | Fire lag med samme scenarier. `ClearDecision_*` alene har ni tester der mange bare varierer ett felt (`UpdatedAt`, datoen, status). |
| Oppgavestatus og `CompletedAt` | `TaskItemTests.ChangeStatus_*` (8), `TaskItemServiceTests.ChangeStatusAsync_*` (8), `TaskItemPersistenceTests.Complete_*/Reopen_*` (4), `TaskDetailTests.Complete_*/Reopen_*` | Samme som over. `TaskItemTests.ChangeStatus_EveryTransition_KeepsCompletedAtInLineWithStatus` dekker allerede alle overganger. |
| Forfalt-regelen og norsk dato rundt midnatt | `TaskItemTests.IsOverdue_*` (7), `TaskItemServiceTests.GetToday_*` og `GetOverdueAsync_*`, `TaskListTests.Render_NearMidnight_*`, `DashboardTests.Render_NearMidnight_*`, `TaskItemPersistenceTests.GetOverdue_UsesTheNorwegianDate…`, `NorwegianTimeTests.DateOf_*` | Midnattsgrensen bør bevises **én gang** (norsk tid), og hver bruker (liste, dashboard) trenger bare vise at den bruker `GetToday()`. |
| Filter og sortering | `TaskItemFilterAndSortingTests` (32) mot `TaskListTests.Filter_*/Sort_*/Search_*` (9); `BoardCaseFilterAndSortingTests` (19) mot `BoardCaseListTests` (5 filter/sort/søk); `HousingUnitFilterTests` (12) mot `HousingUnitListTests.Search_*/StatusFilter_*` | Domene eier reglene. Siden trenger bare å vise at kontrollene er koblet til filteret (én test per kontroll). |
| Dashboardets tall | `DashboardOverviewTests` (20) mot `DashboardTests.Render_Counts*` (4), `FollowUp_*` (4), `Upcoming_*` (3), `BoardCases_*` (2) | Rekkefølge, grenser og telling testes både i `DashboardOverview` og i siden. Siden trenger bare å vise at tallene kommer fra `DashboardOverview` og at tomtilstand og feil vises. |
| Elbillading: «deaktivert lader beholder historikk og teller i avstemmingen» | `ChargingServiceTests.DisabledCharger_KeepsAllItsReadingsAndHistoricalConsumption`, `ChargingReadingSetTests.DisabledCharger_KeepsItsHistory…`, `ChargingReconcilerTests.Reconcile_ADisabledChargerWithHistoricalReadingsInThePeriod…`, `ChargingOverviewTests.Create_DeactivatedChargerKeepsItsLastReading`, `ChargingPersistenceTests.ReadingSet_DisabledChargerKeepsItsHistory…`, `DemoChargingSeedTests.Seed_TheDeactivatedChargerKeepsItsHistoricalConsumption` | Seks steder. Domenet trenger to (avstemming og oversikt), lagringen kan beholde sin (omstart), demoen sin viser at demodataene har dette. Tjeneste- og `ReadingSet`-variantene er trolig overflødige. |
| Elbillading: «laderstatus er uavhengig av boenhetens aktiv-flagg» | `EvChargerTests.Status_IsIndependentOfTheHousingUnit…`, `ChargingOverviewTests.Create_ChargerStatusIsIndependent…`, `ChargingPersistenceTests.ChargerStatus_IsIndependent…`, `ChargingServiceTests.InactiveHousingUnit_DoesNotAffect…` og `RegisterCharger_InactiveHousingUnit_IsAllowed…` | Fem varianter av én regel. Behold tjenestetesten (den har en kontrakt) og lagringstesten. |
| Åtte undermålere i to skap | `ChargingReadingSetTests.SubMeters_SitInCabinet1Or2_FourInEach`, `ChargingServiceTests.RegisterCharger_EightUnits_…`, `ChargingPersistenceTests.Hierarchy_…`, `DemoChargingSeedTests.Seed_PutsFourSubMetersInEachCabinet…`, `ChargingPagesTests.ReadingSetCreate_ShowsMainMeterAndEightSubMeters…` | De tre første tester det samme i tre lag. Demo- og UI-testen tester egne data og eget skjema og er forskjellige. |
| Kronologi (ikke lavere enn forrige) | `MeterHistoryTests.EnsureFits_*` (7) er eieren. I tillegg `ChargingServiceTests.RegisterReading_LowerThanThePreviousReading…`, `ChargingReadingSetTests.RegisterReadingSet_OneValueLowerThan…`, `ChargingPagesTests.ReadingSetCreate_LowerThan…` og `Correction_ValueBreakingChronology…` | Tjenestetesten og én sidetest (feilen vises ved riktig felt) er verdt å beholde. Flere varianter i tjenestelaget kan slås sammen. |
| Atomisk lagring | Fake: `ApproximateReadingsAndAtomicityTests.RegisterCharger_WhenStoringFails_*`, `RegisterReadingSet_WhenStoringFails_*`. Ekte SQLite: `ChargingAtomicityTests.RegisterCharger_DatabaseFailsWhileStoringTheMeter_*`, `ReadingSet_DatabaseFailsOnTheFifthReading_*` | Fake-testene viser at tjenesten ikke skjuler feilen, og de ekte viser at databasen ruller tilbake. Det er to forskjellige ting, men filnavnet `ApproximateReadingsAndAtomicityTests` blander to emner. Se «kan slås sammen». |

### 5.2 Samme oppførsel flere ganger i samme lag eller på tvers av repositories

| Oppførsel | Tester |
|---|---|
| Id gjenbrukes ikke etter sletting | `HousingUnitDeletionTests.Delete_NewestUnit_DoesNotMakeItsIdAvailableAgain`, `BoardCasePersistenceTests.Delete_DoesNotReuseTheDeletedId`, `TaskItemPersistenceTests.Delete_DoesNotReuseTheDeletedId` |
| Sletting av ukjent Id: repository gir `false`, tjenesten kaster | `HousingUnitDeletionTests.Delete_UnknownId_*`, `BoardCasePersistenceTests.Delete_UnknownId_*`, `TaskItemPersistenceTests.Delete_UnknownId_*` |
| Lesing endrer ikke `UpdatedAt` | `BoardCasePersistenceTests.Reading_DoesNotChangeUpdatedAt`, `TaskItemPersistenceTests.Reading_DoesNotChangeUpdatedAt` |
| Tidspunkter lagres som UTC uten forskyvning | `BoardCasePersistenceTests.Timestamps_AreStoredAsUtcTextWithoutTimeZoneShift` **og** `Timestamps_AreUtcAndEqualBeforeAndAfterStorage`; `TaskItemPersistenceTests.Timestamps_AreStoredAsUtcTextWithoutTimeZoneShift`, `CreatedAtAndUpdatedAt_AreUtcAndEqualAfterRestart`, `Complete_CompletedAtIsUtcAndEqualBeforeAndAfterStorage`; `ChargingPersistenceTests.Times_AreUtcAfterReadingAndStoredWithoutTimeZoneShift` |
| Ny rad får positiv Id fra databasen | `EfHousingUnitRepositoryTests.AddAsync_NewUnit_*`, `BoardCasePersistenceTests.Register_GetsPositiveIdFromDatabaseSetOnTheObject`, `TaskItemPersistenceTests.Register_GetsPositiveIdFromDatabaseSetOnTheObject` |
| Ugyldig oppdatering beholder lagrede verdier | `EfHousingUnitRepositoryTests.Service_*`, `HousingUnitDetailsPersistenceTests.UpdateDetails_InvalidValues_*`, `BoardCasePersistenceTests.UpdateDetails_InvalidValue_*`, `TaskItemPersistenceTests.UpdateDetails_InvalidValue_*` (samme regel som `*ServiceTests` og `*Tests.UpdateDetails_InvalidValue_*`) |
| Migrering kjørt to ganger endrer ingenting | `EfHousingUnitRepositoryTests.MigrateAsync_RunTwice_KeepsExistingData`, `BoardCaseMigrationTests.*RunningMigrationsAgain*`, `TaskItemMigrationTests.*RunningMigrationsAgain*`, `ChargingMigrationTests.*RunningAgain*` |
| Skjemaets fremmednøkler er `RESTRICT` | `BoardCaseRelationTests.Schema_ForeignKeyToHousingUnits_IsRestrict`, `TaskItemRelationTests.Schema_ForeignKeys_AreRestrict`, `ChargingMigrationTests.Migrate_CreatesTheUniqueIndexesAndTheRestrictedForeignKeys` |

Disse er likevel ikke helt identiske: hver gjelder en egen tabell og egen mapping, og migreringstestene beskytter hvert sitt skjemasteg. Derfor er forslaget å **parameterisere** (én teori med en rad per entitet) og ikke å fjerne.

### 5.3 Tester av fake-implementasjonen og trivielle tester

Disse tester i praksis fake repository eller språk/rammeverk, ikke produktet:

| Test | Hvorfor lavverdi |
|---|---|
| `HousingUnitServiceTests.RegisterAsync_TwoUnits_AssignsDifferentIds`, `BoardCaseServiceTests.RegisterAsync_TwoCases_GetDifferentIds`, `TaskItemServiceTests.RegisterAsync_TwoTasks_GetDifferentIds` | Id-tildeling skjer i fake-en. Den ekte Id-tildelingen er testet i lagringslaget. |
| `*ServiceTests.GetAllAsync_No*_ReturnsEmptyList`, `GetAllAsync_Two*_ReturnsBoth`, `GetByIdAsync_*` (12 tester) | Tjenesten delegerer bare. Det samme finnes i `*PersistenceTests.GetAll_*`/`GetById_UnknownId_ReturnsNull`/`EfHousingUnitRepositoryTests.*`. |
| `AssignId_PositiveId_SetsId`, `AssignId_ZeroOrNegative_Throws`, `AssignId_AlreadyAssigned_Throws…` i `HousingUnitTests`, `BoardCaseTests`, `TaskItemTests`, `EvChargerTests`, `MeterAndFacilityTests`, `MeterReadingTests` | Samme regel (`TextRules.EnsurePositiveId`) seks ganger. Reglene er riktige å teste, men kan være én teori. |
| `HousingUnitTests.Id_NewUnit_IsZeroUntilAssigned`, `BoardCaseTests.DecisionDate_NewCase_IsNull` | Tester standardverdier som også dekkes av `Constructor_OnlyX_UsesDefaults…`. |
| `BoardCaseTests.ToDetails_ReturnsCurrentValues`/`ToDetails_UsedForUpdate_ChangesNothing`, tilsvarende i `TaskItemTests` | Delvis trivielt, men `ToDetails_UsedForUpdate_ChangesNothing` har verdi fordi redigeringsskjemaene bruker mønsteret. Behold den, slå sammen den andre med konstruktørtesten. |
| `BoardCaseCreateTests.Render_HasLinkBackToList`, `TaskCreateTests.Render_HasLinkBackToList`, `HousingUnitCreateTests.Render_HasLinkBackToList`, `TaskEditTests.Render_HasLinksBackToTheTask`, `Render_Task_HasEditAndBackLinks`, `Render_Case_HasEditAndBackLinks`, `Render_Unit_HasEditLinkAndBackLink`, `TaskListTests.Render_HasRegisterButtonInTheHeader`, `Render_Task_HasOpenAndEditLinks` | Kontrollerer at en lenke finnes. Kan samles i én «navigasjon»-test per side. |
| `NorwegianDateFormatTests.DateTime_SummerTime_*`, `DateTime_WinterTime_*`, `DateTime_LateEveningUtc_*` | Gjentar `NorwegianTimeTests.FromUtc_Summer/Winter` og `DateOf_ChangesAtNorwegianMidnight`; bare formateringen er ny. |
| `EfHousingUnitRepositoryTests.Service_*` (4 tester) | Tjenestetestene (`HousingUnitServiceTests`) mot ekte SQLite. Ingen ekstra regel. |

`DefaultDatabasePath_IsInsideLocalAppData` ser trivielt ut, men beskytter mot at appen havner i feil database. **Behold.**

### 5.4 Demotestene

`DemoDatabaseSeederTests` har 54 kjørte tester. Omtrent 14 av dem er **øyeblikksbilder av demoens innhold**, for eksempel `Seed_Owners_AreMixedAndFictional`, `Seed_ContactDetails_AreClearlyFictional`, `Seed_ContainsTheTopicsFromTheRequirements`, `Seed_Tasks_HaveTheRequestedSpreadOfDueDates`, `Seed_CompletedTasks_HaveCompletionTimesInThePast`, `Seed_BoardCases_HavePastAndUpcomingMeetingsAndMixedLinks`, `Seed_Tasks_HaveAllKindsOfLinksAndAssignees`, `Seed_GivesTheDashboardRealisticNumbers`. De sier mer om hvilke demodata som ble valgt enn om at programmet virker, og de bryter hver gang demoinnholdet justeres. Noen få invarianter har verdi (alle koblinger peker på eksisterende rader, ingen tider frem i tid, alle telefon/e-post er fiktive). Sikkerhetstestene (stien, delvis seed, egne data, feil midt i opprettelsen) skal beholdes.

## 6. Skjøre eller implementasjonsnære tester

| Mønster | Omfang | Risiko | Forslag |
|---|---|---|---|
| Forventede **norske tekster** i UI-tester (`Assert.Contains("kan ikke gå ned", …)`) | ca. 268 `Assert.Contains("…")` i `Web/` | Endrer du ordlyden, feiler mange tester selv om oppførselen er riktig. | Behold for feilmeldinger som er selve kravet. Test ellers på `role="alert"`/`aria`-tilstand eller på dataene, ikke ordlyden. |
| **CSS-klasser og markup-struktur** (`.stat-card`, `.data-table`, `tbody tr`, `.stat-row`, `ParentElement`) | ca. 28 steder med klasse/rad-velgere og 8 `ParentElement`-kjeder, mest i `ChargingPagesTests`, `DashboardTests`, `TaskListTests` | Stilendringer og markup-omskriving knekker testene. | Små hjelpere i `UiTestBase` (for eksempel «finn rad med tekst», «finn felt med etikett»). Ev. `data-testid` på noen få nøkkelelementer (krever en liten kodeendring, så den trenger godkjenning). |
| **Faste datoer og klokkeslett** | 34 forekomster av `new DateTime(2026, …)` | Akseptabelt: klokken styres av `ManualTimeProvider`, ikke av dagens dato. Dette er ikke et problem, bare noe å vite om. | Ingen endring. |
| **Demoplanens tall** i tester (`44` avlesninger, `1250/1220`, `InRange(90, 150)`) | `DemoChargingSeedTests` | Endres planen, feiler testene. Det er delvis hensikten, siden de skal vise at kontrollen sammenligner innhold. | Hent forventede tall fra `DemoChargingPlan` der testen ikke handler om selve tallene. |
| **Rå SQL med tabellnavn** | `DemoChargingSeedTests.RemoveAllChargingDataAsync`, migreringstestene | Følger skjemaet, og er riktig for migreringstester. | Ingen endring. |
| **Gjenta oppsett** | `UiTestBase` har en tung konstruktør, og mange tester lager data for hånd | Lange tester, vanskelig å se hva som er viktig. | Små byggerhjelpere (for eksempel `RegisterUnitAsync`, `RegisterTaskAsync`). Flere finnes allerede. |
| **Sletting av tabellrader med SQL for å simulere «eldre demo»** | `DemoChargingSeedTests.Seed_OlderDemoWithoutChargingData_*` | Tester at kontrollen oppfører seg riktig, men et skjema-endring i fremtiden kan bryte SQL-en. | Behold; testen ved siden av (`…MigratedFromTheSchemaBeforeCharging…`) er den realistiske. |
| `SqliteConstraintErrorsTests` (12) | Tester at feilkoder fra `Microsoft.Data.Sqlite` klassifiseres riktig | Knyttet til bibliotekets feilkoder, men beskytter en tidligere oppdaget feil. | Kan slås sammen til to teorier (`IsForeignKeyViolation`, `IsUniqueViolation`). |

## 7. Er testpakken for komplisert for et læringsprosjekt?

**Det som fungerer godt:**
- Domenetestene er enkle: ingen database, ingen UI, kjører på 0,2 s, og navnet beskriver regelen.
- Fire tydelige nivåer (domene, tjeneste med fake, SQLite, side) med en klar tanke bak hver.
- `ManualTimeProvider` og `SqliteTestDatabase` er små og forklarbare.

**Det som gjør den tyngre enn nødvendig:**
1. **Lagdelt gjentakelse** (punkt 5.1): hver regel skrives fire ganger. For en nybegynner er det uklart hvilken test som er «den ekte».
2. **Lange testnavn** (mange over 80 tegn, for eksempel `ReadingSet_DatabaseFailsOnTheLastReading_NoneStayEvenThoughEightWereInserted`). De er presise, men vanskelige å skanne i en liste.
3. **Store filer:** `BoardCaseTests` (816 linjer), `TaskItemServiceTests` (791), `ChargingPagesTests` (769). Én fil per klasse er greit, men 800 linjer er tungt å navigere.
4. **To nesten like testlag** for tjenestene: tjeneste med fake og tjeneste mot ekte SQLite (`EfHousingUnitRepositoryTests.Service_*`).
5. **Demoseeding i hver test** gjør at en liten endring i demoen gir langsom tilbakemelding.

Ingen av disse er et argument for å fjerne tester. De er et argument for **tydeligere regler om hvor en test hører hjemme** (punkt 10).

## 8. Manglende tester som gir økt trygghet

Prioritert etter hvor sannsynlig og hvor skadelig feilen er.

| # | Mangel | Hvorfor den er viktig | Foreslått test |
|---|---|---|---|
| 1 | **Ingen test av `Program.cs`**: DI-registrering, migrering ved oppstart og demokommandoene (`--seed-demo`, `--check-demo`, exit-koder) | `SqliteTestDatabase` bygger sin egen DI-container, så en glemt `AddScoped<…>` i `Program.cs` oppdages ikke av testene. Det er også eneste sted demostien sperres. | En røyktest: start appen mot en midlertidig database (for eksempel med `WebApplicationFactory`, som krever pakken `Microsoft.AspNetCore.Mvc.Testing`, og må derfor godkjennes), hent `/` og `/elbillading` og forvent 200. Alternativt en enkel prosesstest i CI. |
| 2 | **Sidene testes aldri mot ekte SQLite.** bUnit bruker fake repositories. Feil i EF-mapping, lagringsformat og sidenes samspill med databasen slipper gjennom (feilen med `datetime-local` er et eksempel på en feil i grensen mellom side og input). | Brukeren opplever siden og databasen sammen. | 5–8 bUnit-tester med ekte EF-repositories for de viktigste flytene: registrer boenhet → styresak → oppgave og se dem på dashboardet; registrer lader og avlesningssett og se avstemmingen; korriger en avlesning. |
| 3 | **Dobbeltklikk-vern testes bare for `TaskCreate`** (`Submit_WhileTheFirstSaveIsStillRunning_*`). Andre skjema (`ReadingSetCreate`, `ChargerForm`, boenhet, styresak) har samme `_isSaving`-mønster, men ingen test. | Dobbel registrering av en avlesning er en realistisk feil, og kan gi duplikat. | Én teori per skjema, eller en delt hjelper. |
| 4 | **Feiltilstander i de nye elbillingsseksjonene:** `UnitChargingSection` og `DashboardChargingSection` har `catch`-grener, men ingen test av at en lesefeil viser melding uten å velte hele siden. | Dashboardet er første side brukeren ser. En feil i ladedata skal ikke gjøre den tom. | To små tester med `Readings.FailOnRead = true`. |
| 5 | **Brukerflyt: eldre demo uten ladedata i grensesnittet** (setup-skjema vises, og brukeren kan sette opp anlegget manuelt) | Er en realistisk situasjon for en som har en gammel demo. | En test som rendrer `ChargingIndex` uten anlegg og setter opp anlegget, og deretter registrerer en lader (finnes delvis i `Index_WithoutFacility_OffersSetupAndCreatesFacility`; mangler koblingen til første lader og avlesning). |
| 6 | **Skript (`demo.ps1`, `Start-demo.cmd`, `Nullstill-demo.cmd`)** har ingen automatiske tester | `reset` sletter filer. Sperren (`Assert-IsDemoDatabasePath`) er sikkerhetskritisk. | Pester-tester for `Assert-IsDemoDatabasePath` og `Test-DemoPortInUse`, eller en skriftlig manuell sjekkliste i `docs/TESTING.md` (billigere). |
| 7 | **Kulturavhengighet** (norsk/engelsk tallformat, datoformat) | Testene går grønt både lokalt (nb-NO) og i CI (en-US), så det finnes implisitt dekning, men ingen test som sier at det er meningen. | Ingen ny test nå. Nevn det i `TESTING.md`, og kjør ved behov med `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT`. |
| 8 | **Backup og gjenoppretting** (M5, obligatorisk før v1.0) finnes ikke ennå | Dette blir den viktigste integritetsfunksjonen. | Skriv testene først (se TDD-forslaget): gjenoppretting gir identiske data, en feilet gjenoppretting etterlater den gamle databasen uendret, og backup av en database som brukes. |
| 9 | **Reell nettleserflyt** | bUnit kjører ikke ekte nettleserhendelser. Feilen med `datetime-local` og `Blazor.navigateTo` ble bare funnet manuelt. | Ikke innfør Playwright nå (ny avhengighet og vedlikehold). Behold den manuelle nettlesersjekklisten i kontrollrapportene, og vurder Playwright først hvis flere slike feil dukker opp. |

Jeg anbefaler **ikke** tester av samtidighet mellom flere brukere: programmet har én lokal bruker.

## 9. Hva som bør beholdes, slås sammen og eventuelt fjernes

### Beholdes (ikke rør)

- Alle tester i punkt 4: kritiske regler, transaksjoner, rollback, migrering, fremmednøkler, `CHECK`/unike begrensninger og regresjonstester.
- Grenseverdi-variasjon: `UpdateDetails_ValuesAtTheLimits_*`, `RegisterDecision_DatesAtTheLimits_*`, `FindNearest_*`, `EnsureFits_*`, `MeterReadingTests.Create_*`.
- `Architecture/DependencyDirectionTests`.
- Sikkerhetstestene for demo: stien, delvis seed, egne data, feil midt i opprettelsen, `Seed_OlderDemo*`.
- Én UI-test per virkelig brukerhandling (opprett, rediger, slett med bekreftelse, statusendring, feilmelding, tomtilstand, databasefeil).

### Kan slås sammen (samme dekning, mindre kode)

| Hvor | Forslag | Verdi |
|---|---|---|
| `BoardCaseTests` og `TaskItemTests`: `UpdateDetails_InvalidX_ThrowsAndChangesNothing` (5 og 7 varianter) | Én teori per entitet med verdi og forventet feltnavn | Mindre kode, samme grenseverdier, lettere å legge til en ny regel |
| `BoardCaseTests.ClearDecision_*` (9) | Fire tester: fjerner vedtak og dato; avvist når vedtatt; ingenting å fjerne endrer ingenting; `UpdatedAt` kun ved reell endring | Fjerner overlapp mellom `…SetsUpdatedAt`/`…DoesNotChangeUpdatedAt`/`…KeepsDecisionAndDate` |
| `UpdateDetails_SameValues_*` og `…WithExtraSpaces_*` i tre entiteter | Teori «uendret verdi (også med mellomrom) endrer ikke `UpdatedAt`» | Samme regel, én plass |
| `AssignId_*` i seks klasser | En delt teori eller hjelper per regel | Regelen er felles (`TextRules`) |
| `ChargingServiceTests` og `ChargingReadingSetTests`: de to «deaktivert lader»-testene, «inaktiv boenhet»-testene og «åtte undermålere»-testen | Behold én i domenet, én i lagring og én i demo (se 5.1) | Færre steder å endre når regelen endres |
| `ApproximateReadingsAndAtomicityTests` (to emner) | Del i `ApproximateReadingsTests` (til `ChargingReconcilerTests`/`MeterHistoryTests`) og flytt de to atomisitetstestene til `ChargingServiceTests` | Filnavn som sier hva som ligger der |
| `*MigrationTests.*RunningMigrationsAgain*`, `Schema_ForeignKeys_AreRestrict` | Parameteriser over entitet | Samme beskyttelse, én mal |
| `SqliteConstraintErrorsTests` (12) | To teorier | Samme dekning, kortere |
| `Render_Has…Link…` (9 tester) | Én «navigasjon»-test per side | Fjerner trivielle enkelt-tester |
| `NorwegianDateFormatTests` | Én teori | Samme |
| `*PersistenceTests.Timestamps_*` (2 i `BoardCasePersistenceTests`, 3 i `TaskItemPersistenceTests`) | Én test per entitet som sjekker lagringsformat og at verdien er uendret etter omstart | Samme regel testes fem ganger |
| Demo-innholdstester i `DemoDatabaseSeederTests` | Samle i 3–4 invariant-tester som deler én seedet database (se plan) | Raskere og mindre skjørt |

### Kan eventuelt fjernes (etter å ha lest testkroppen)

Dette er de eneste jeg vurderer som kandidater til fjerning, og bare hvis kroppen bekrefter at de ikke gir noe utover testene de overlapper:

| Test | Hvorfor |
|---|---|
| `HousingUnitServiceTests.RegisterAsync_TwoUnits_AssignsDifferentIds`, `BoardCaseServiceTests.RegisterAsync_TwoCases_GetDifferentIds`, `TaskItemServiceTests.RegisterAsync_TwoTasks_GetDifferentIds` | Tester fake-ens Id-tildeling |
| `*ServiceTests.GetAllAsync_No*_ReturnsEmptyList` og `GetAllAsync_Two*_ReturnsBoth` | Tjenesten delegerer, og lagringslaget tester det samme på ekte database |
| `HousingUnitTests.Id_NewUnit_IsZeroUntilAssigned`, `BoardCaseTests.DecisionDate_NewCase_IsNull` | Dekkes av konstruktørtestene |
| `NorwegianDateFormatTests.DateTime_SummerTime_*` og `DateTime_WinterTime_*` | Dekkes av `NorwegianTimeTests.FromUtc_*` (men behold én formateringstest) |
| `ChargingServiceTests.RegisterCharger_EightUnits_…` og `ChargingReadingSetTests.SubMeters_SitInCabinet1Or2_FourInEach` | Samme som `ChargingPersistenceTests.Hierarchy_…` |
| `EfHousingUnitRepositoryTests.Service_*` | Samme som `HousingUnitServiceTests`, bare mot SQLite |

Ingenting i listen er en kritisk regel, en transaksjonstest, en migreringstest, en integritetstest eller en regresjonstest.

## 10. Enklere TDD-arbeidsflyt for videre utvikling

Poenget er at **hver regel får én hjemmetest i det laveste laget som kan eie den**, og at de øvrige lagene bare viser at reglen er koblet inn.

### Hvor hører testen hjemme?

| Du skriver … | Skriv testen i … | Hva den beviser | Hva du ikke trenger |
|---|---|---|---|
| En ny regel (validering, beregning, tilstandsendring) | **Domain** (entitet eller ren klasse). Bruk `[Theory]` for grenseverdier. | Regelen, inkludert feilsti og at ingenting endres ved feil. | Samme regel i tjeneste, lagring og side. |
| En ny tjenesteoperasjon som kombinerer flere ting (kobling, flere repositories) | **Domain-tjeneste med fake**: én vellykket sti, én avvist sti som ikke endrer noe | Samspillet og at feil ikke gir delvis lagring | Gjentakelse av entitetens regler |
| En ny kolonne, relasjon, constraint, migrering eller transaksjon | **Infrastructure mot ekte SQLite** | Kartlegging, at databasen selv stopper brudd, rollback | Tester av regler som allerede ligger i domenet |
| En ny side eller handling | **bUnit**: én vellykket handling, én feil (databasefeil eller ugyldig verdi), tom/ikke-funnet-tilstand. Vis tydelig hvilken tjeneste siden kaller. | At siden kobler UI og tjeneste og viser feil | Hele regelmatrisen. Én test per kontroll holder. |
| En feil du har funnet | Første test på **det laveste laget som kan reprodusere den**, ev. én ekstra i grenselaget (for eksempel `datetime-local`) | At feilen ikke kommer tilbake | – |

### Arbeidsflyt per delmål

1. **Skriv regelen som en setning** («Avlesning kan ikke være lavere enn forrige»).
2. **Velg lag** med tabellen over.
3. **Skriv testen først, og se den feile av riktig grunn** (ikke kompileringsfeil på noe annet enn det du skal lage).
4. **Gjør den grønn med minst mulig kode.**
5. **Refaktorer med testene grønne.**
6. **Legg til bare de grenseverdiene som er forskjellige** (ikke samme sak med nytt navn).
7. **Ett koblingstesttrinn per lag over:** tjeneste (én sti), side (én sti), ikke flere.
8. **Kjør alle testene før commit** (`dotnet build`, `dotnet test`).

### Konvensjoner som holder pakken enkel

- **Navn:** `Metode_Situasjon_Forventning`, maks omtrent 70 tegn. Detaljene ligger i testen.
- **Filstørrelse:** del en klasse når den passerer omtrent 400 linjer, etter atferd (for eksempel `BoardCaseDecisionTests`).
- **Delt oppsett:** bruk `IClassFixture` for dyre oppsett (seedet demo) og bygg vanlige data med små hjelpere.
- **Hjelpere for UI:** «finn rad som inneholder tekst», «finn felt med etikett», så testene ikke kjenner til markup.
- **Spor langsomme tester:** `[Trait("Category", "Slow")]` på tester som seeder demo eller bruker SQLite i stort omfang, slik at man lokalt kan kjøre `dotnet test --filter "Category!=Slow"` mens man utvikler, og alt i CI. Dette endrer ikke hva som kjøres i CI.
- **Mål ikke antall.** Mål i stedet: Er hver regel bevist på ett sted? Feiler en test hvis og bare hvis oppførselen er brutt?

## 11. Prioritert plan

Hvert steg kan godkjennes for seg. Ingen endring gjøres før du godkjenner.

### Prioritet 1: stor verdi, lav risiko

| # | Tiltak | Hvorfor |
|---|---|---|
| 1 | **Gjør demotestene raske med en delt, skrivebeskyttet fixture** (seed én gang, kjør de rene lesetestene mot den; tester som endrer data beholder egen database). **Ingen tester fjernes.** | Spart tid: opp mot 25 av ca. 45 sekunder. Raskere tilbakemelding gir bedre TDD. Samme tester, samme dekning. |
| 2 | **Røyktest av oppstart og DI** (`Program.cs`): appen starter mot midlertidig database og returnerer 200 på `/`, `/elbillading`. | Fanger «glemt registrering», migreringsfeil og at demokommandoene avviser feil sti. Krever en testpakke (`Microsoft.AspNetCore.Mvc.Testing`), så trenger godkjenning. |
| 3 | **5–8 sidetester mot ekte SQLite** for hovedflytene (punkt 8, nr. 2). | Fanger feil i grensen side/lagring som fake-repositories ikke ser. |
| 4 | **Fyll de små hullene:** feiltilstand i `UnitChargingSection`/`DashboardChargingSection`, dobbeltklikk-vern på `ReadingSetCreate` og de andre skjemaene. | Billige tester som beskytter realistiske feil. |

### Prioritet 2: reduser duplisering uten å miste dekning

| # | Tiltak | Hvorfor |
|---|---|---|
| 5 | **Slå sammen til teorier** i `BoardCaseTests`/`TaskItemTests` (ugyldige verdier, uendrede verdier, `ClearDecision_*`), `AssignId_*`, `SqliteConstraintErrorsTests`, migreringstester, `Render_Has…Link…`. | Samme dekning med mye mindre kode. Én mal å utvide neste gang. |
| 6 | **Konsolider elbilladingsdupliseringer** etter tabellen i 5.1: «deaktivert lader», «laderstatus uavhengig av boenhet», «åtte undermålere». | Færre steder å endre. Kritiske regler beholdes i ett lag. |
| 7 | **Fjern de seks kandidatene i punkt 9** etter å ha lest hver testkropp. | Mindre støy. Ingen kritisk regel berøres. |
| 8 | **Skill ut elbilladingsfil:** del `ApproximateReadingsAndAtomicityTests` etter emne. | Filnavn som sier hva som ligger der. |
| 9 | **Reduser demotestenes øyeblikksbilder** (14 → 3–4 invarianter). | Mindre skjørhet når demoinnholdet endres. |

### Prioritet 3: lettere å vedlikeholde

| # | Tiltak | Hvorfor |
|---|---|---|
| 10 | **UI-hjelpere** (`FindRowWithText`, `FindFieldByLabel`) og ev. noen `data-testid` (krever godkjenning for kodeendring). | Færre brutte tester ved layoutendringer. |
| 11 | **Del store testfiler** (>600 linjer) etter atferd. | Lettere å finne frem for en nybegynner. |
| 12 | **Skriv strategien (punkt 10) inn i `docs/TESTING.md`**, inkludert «hvor hører testen hjemme»-tabellen. | Gjør neste milepæl enklere å teste riktig. |
| 13 | **Mål kodedekning én gang** (`coverlet`), uten krav. | Viser faktiske hull, ikke antakelser. |

### Prioritet 4: før M5 (backup og gjenoppretting)

| # | Tiltak | Hvorfor |
|---|---|---|
| 14 | **Skriv integritetstestene for backup/gjenoppretting først** (identiske data, feilet gjenoppretting påvirker ikke den gamle databasen, backup mens databasen brukes), og vurder Pester-tester eller en sjekkliste for skriptene. | M5 er den mest dataintegritetskritiske milepælen og passer best med å skrive testene først. |

### Forventet effekt

- **Tid:** testkjøring ned fra ca. 45 s til trolig under 20 s, nesten alt fra steg 1.
- **Antall:** et lavere antall som følge av steg 5–9, men det er en bivirkning og ikke et mål. Kritiske tester endres ikke.
- **Trygghet:** øker med steg 2–4 og 14, fordi de dekker feil testene i dag ikke ser (oppstart, side mot ekte database, skript, backup).

## 12. Hva dette dokumentet ikke har gjort

- Ingen test er slettet, skrevet om eller lagt til.
- Ingen applikasjonskode eller arkitektur er endret.
- Ingen avhengighet (for eksempel `Mvc.Testing` eller Pester) er lagt til.
- Ingen milepæl er startet. Neste skritt er din godkjenning av hvilke steg i punkt 11 som skal gjennomføres.
