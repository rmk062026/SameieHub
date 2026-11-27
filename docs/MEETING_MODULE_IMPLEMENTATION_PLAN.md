# Møtemodulen: arbeidsrutine og foreløpig implementasjonsplan

**Status: forslag.** Dette er en teknisk plan for å lære og bygge modulen i små steg. Den er **ikke en funksjonsspesifikasjon**. Prosjekteieren og ChatGPT bestemmer kravene før implementeringen begynner (MM0). Ingen møtekode er skrevet.

Relaterte dokumenter: [læringsguiden](MEETING_MODULE_LEARNING_GUIDE.md), [arkitekturen](MEETING_MODULE_ARCHITECTURE.md), [kodereferansen](MEETING_MODULE_REFERENCE.md).

> **Hvem skriver koden:** for møtemodulen (MM0–MM11) er prosjekteieren hovedutvikler og skriver produksjonskoden selv, med ChatGPT som veileder. Claude Code kartlegger, dokumenterer og kontrollerer, og implementerer ingenting uten uttrykkelig godkjenning. Selvskrevet kode og AI-generert implementasjon holdes atskilt når det er praktisk mulig (se 1.10). Se også «Unntak: møteadministrasjonen» i `CLAUDE.md`.

---

## Del 1. Utviklingsmiljø og arbeidsrutine

Alle kommandoer kjøres fra rotmappen (`SameieHub`), i PowerShell.

### 1.1 Forutsetninger
Windows, .NET 10 SDK, Git, GitHub CLI (`gh`), Visual Studio 2026 (eller annen editor). Verktøyet `dotnet-ef` er definert i `dotnet-tools.json`:

```powershell
dotnet tool restore
```

### 1.2 Åpne i Visual Studio
1. Åpne `SameieHub.slnx` (Fil > Åpne > Prosjekt/løsning).
2. Høyreklikk **`SameieHub.Web`** > «Sett som oppstartsprosjekt».
3. Velg profilen **`http`** (`http://localhost:5254`) eller `https` (`https://localhost:7113`) fra `Properties/launchSettings.json`. F5 starter med feilsøking, Ctrl+F5 uten.
4. Test Explorer (Test > Test Explorer) kjører testene.

### 1.3 Vanlig utviklingsmodus fra terminalen

> **Viktig:** uten noe annet bruker utviklingsmodus **den ordinære databasen** `%LOCALAPPDATA%\SameieHub\sameiehub.db`. Bruk alltid en egen testdatabase mens du utvikler:

```powershell
$env:Database__Path = "$env:TEMP\sameiehub-mote-dev.db"
```
```powershell
dotnet run --project src/SameieHub.Web
```

Etterpå (så neste start ikke arver den):

```powershell
Remove-Item Env:Database__Path
```

I Visual Studio kan du legge `Database__Path` i en **egen** profil i `launchSettings.json` – ikke commit den endringen (sjekk `git status`), eller bruk en brukerfil.

**Kontroller at riktig database brukes:** ved oppstart skriver programmet `Using database <mappe>/<fil>` (se `Program.cs`). Les den linjen før du legger inn noe.

### 1.4 Demoen

```powershell
.\scripts\Start-demo.cmd
```

(eller `.\scripts\demo.ps1 launch`; `create`, `start`, `reset`, `info` finnes også). Demoen bruker `%LOCALAPPDATA%\SameieHubDemo\sameiehub-demo.db` på port 5100, og ordinærdatabasen røres ikke. Blokkerer Windows skriptet, bruk `.cmd`-filen. Se [DEMO.md](DEMO.md).

### 1.5 Windows-modus (bare ved behov)
Se `DEVELOPMENT.md`, «Windows-modus». Trengs ikke for å utvikle modulen.

### 1.6 Bygg og test

```powershell
dotnet build SameieHub.slnx
```
```powershell
dotnet test SameieHub.slnx
```
```powershell
dotnet test SameieHub.slnx --filter "FullyQualifiedName~BoardCaseServiceTests"
```

Kravet før commit: 0 advarsler, alle tester grønne (i dag 1733). **Stopp programmet** (Ctrl+C eller stopp feilsøking) før du bygger, ellers er filene låst.

### 1.7 EF Core-migreringer

```powershell
dotnet ef migrations add AddMeetings --project src/SameieHub.Infrastructure --output-dir Persistence/Migrations
```
```powershell
dotnet ef migrations list --project src/SameieHub.Infrastructure
```
```powershell
dotnet ef migrations has-pending-model-changes --project src/SameieHub.Infrastructure
```

- `list` viser «(Pending)» for alt: det er normalt. Verktøyet bruker `DesignTimeDbContextFactory`, som peker på en kastefil (`design-time.db`), **ikke** din database. Derfor brukes ikke `dotnet ef database update`.
- **Migreringene anvendes automatisk når programmet starter** (og av testene).
- Angre en migrering som **ikke er committet/anvendt på ekte data**: `dotnet ef migrations remove --project src/SameieHub.Infrastructure`.
- Les den genererte filen før commit (spesielt `onDelete: ReferentialAction.Restrict` og kolonner uten standardverdi). Endre aldri en migrering som allerede er brukt.
- `*.db`-filer er ignorert av git.
- **Før du kjører en ny migrering mot noe du bryr deg om:** lag en sikkerhetskopi (siden «Sikkerhetskopi»).

### 1.8 Testdatabaser og isolasjon
- Enhetstester: fake repositories, ingen database.
- Integrasjonstester: `SqliteTestDatabase.CreateAsync()` lager en database i en unik midlertidig mappe, migrerer den og sletter den etterpå. `CreateUnmigratedAsync` + `MigrateToMigrationAsync("…")` for å teste oppgradering fra eldre skjema.
- bUnit: `UiTestBase` med fakes.
- Tester skal **aldri** bruke `%LOCALAPPDATA%\SameieHub`. Testdata er fiktive.

### 1.9 Git og GitHub Actions
- Repo `rmk062026/SameieHub`. **MM1–MM11 implementeres på grenen `feature/meeting-management`.** MM0 og dokumentasjonsendringer kan gjøres på `main`. Grenen opprettes først når du skal begynne på MM1.
- Før hver push:

```powershell
git status
```
```powershell
git config user.name; git config user.email; gh api user --jq .login
```
```powershell
git remote -v
```

- Commit-meldinger: Conventional Commits (`feat:`, `test:`, `fix:`, `docs:`, `refactor:`). Små, meningsfulle commits, helst «test + kode» sammen. Aldri force-push. Aldri databaser, sikkerhetskopier, personopplysninger eller hemmeligheter.
- Opprette grenen (én gang, ved starten av MM1, fra en oppdatert `main`):

```powershell
git switch main
```
```powershell
git pull
```
```powershell
git switch -c feature/meeting-management
```
```powershell
git push -u origin feature/meeting-management
```

- Senere push fra grenen: `git push`. Push fra `main` (MM0 og dokumentasjon): `git push origin main`. Sjekk alltid med `git branch --show-current` hvilken gren du står på.

- GitHub Actions (`.github/workflows/ci.yml`) kjører `build-and-test`, `windows-package` og `windows-installer` på push til `main` og på pull requests. **En push til `feature/meeting-management` starter ikke CI alene.** Åpne en pull request (gjerne som utkast) mot `main`, så kjøres CI for hver push til grenen. Se CI for `main`:

```powershell
gh run list --branch main --limit 3
```
```powershell
gh run watch
```

### 1.10 Opphav til kode (eier og AI)
- Kode du skriver selv, committer du normalt uten AI-medforfatter.
- Kode Claude implementerer, ligger i egne commits og merkes med `Co-Authored-By` der det er reelt medforfatterskap.
- Kode ChatGPT genererer og du kopierer direkte inn, dokumenteres også som AI-bidrag. Si det i commit-meldingen, og commit den gjerne for seg, for eksempel:

```powershell
git commit -m "feat: møtestatus (MM2)" -m "AI-bidrag: ChangeStatus er generert av ChatGPT og kopiert inn."
```

- Veiledning, forklaringer og vanlig feilsøkingshjelp gjør ikke i seg selv AI til medforfatter.
- Skill de to når det er praktisk mulig.

---

## Del 2. Mulig faglig struktur (forslag, ikke vedtatt)

| Område | Grunnfunksjon (v1) | Mulig senere utvidelse |
|---|---|---|
| Styremøter | Opprette, redigere, slette; tittel, dato, klokkeslett, sted, status | Flere møtetyper (sameiermøte/årsmøte), møteserier, maler |
| Agenda og saksliste | Nummererte punkter med tittel, rekkefølge | Dra-og-slipp, tidsestimat, standardpunkter |
| Kobling til styresaker | Et punkt kan peke på en eksisterende styresak | Samme sak i flere møter med historikk |
| Deltakere og fremmøte | Navn og «møtt / ikke møtt» | Faste styremedlemmer, roller (leder, referent), signatur |
| Referat | Fritekst per møte eller per punkt | Godkjenning og låsing, versjoner, utskrift/PDF |
| Vedtak | Registreres på tilknyttet styresak | Egne vedtak uten styresak, vedtaksprotokoll |
| Oppfølgingsoppgaver | «Ny oppgave» fra et punkt, forhåndsutfylt med styresaken | Oppgaver koblet direkte til møtet, påminnelser |
| Møtehistorikk | Liste over tidligere møter, sortert og filtrert | Søk i referater, statistikk |

**Utenfor v1 uansett** (følger `CLAUDE.md`): e-post/innkalling til andre, dokumentarkiv/vedlegg, flere brukere, eksterne integrasjoner.

---

## Del 3. Milepælplan (MM0–MM11)

Hver milepæl er liten nok til at studenten skriver koden selv. Commit når akseptansekriteriene er oppfylt. Rekkefølgen er domene → tjeneste → lagring → UI. Navn (`Meeting` osv.) er **eksempler** til MM0 er avgjort.

For alle milepæler gjelder: skriv testen først, se den feile, implementer, kjør `dotnet build` og `dotnet test`, commit.

### MM0. Krav og datamodell (ingen kode)
- **Lage:** et kort kravdokument (felter, regler, statuser, hva som skal med i v1) og en skisse av datamodellen. Besvar «Åpne beslutninger».
- **Teori:** entiteter og relasjoner (1–mange, mange–mange), normalisering på enkelt nivå.
- **Les:** `BoardCase.cs`, `TaskItem.cs`, `REQUIREMENTS.md`, arkitekturdokumentets del 7.
- **Skriv selv:** kravdokumentet (f.eks. `docs/MEETING_REQUIREMENTS.md`).
- **Tester:** ingen.
- **Akseptanse:** eieren har godkjent krav og modell skriftlig; spørsmålet om `BoardCase.MeetingDate` er besvart.
- **Vanlige feil:** å starte med tabeller før reglene; å bygge «alt» i v1.
- **Spørsmål:** Hva er et møte i programmet, og hva er ikke? Hvem eier datoen: møtet eller saken?
- **Commit:** `docs: møtekrav og datamodell`.

### MM1. Entiteten `Meeting` (domene, TDD)
- **Lage:** `Meeting`, `MeetingDetails`, konstanter, konstruktør, `UpdateDetails`, `ToDetails`, `AssignId`.
- **Teori:** læringsguiden 2, 3, 5, 6, 7.
- **Les:** `BoardCase.cs`, `BoardCaseDetails.cs`, `Domain/BoardCaseTests.cs`, `TextRules.cs`.
- **Skriv selv:** `src/SameieHub.Domain/Meeting.cs`, `MeetingDetails.cs`, `tests/…/Domain/MeetingTests.cs`.
- **Tester:** standardverdier, påkrevd tittel, trimming, for lang tekst, datogrenser, UTC-krav, `UpdatedAt` bare ved endring.
- **Akseptanse:** alle regler fra MM0 har en test; ugyldige verdier endrer ingenting.
- **Vanlige feil:** tilordne før alt er validert; bruke `DateTime.Now`; skrive feltnavn som streng i stedet for `nameof`.
- **Spørsmål:** Hvorfor privat konstruktør? Hva skjer hvis to felt er ugyldige?
- **Commit:** `feat: møte-entitet med regler (MM1)`.

### MM2. Status og regler
- **Lage:** `MeetingStatus` (verdier fra MM0) og `ChangeStatus`.
- **Teori:** guide 4.
- **Les:** `BoardCase.ChangeStatus`, `TaskItem.ChangeStatus`.
- **Skriv selv:** enum, metode, tester.
- **Tester:** gyldige og ugyldige overganger; samme status endrer ingenting.
- **Akseptanse:** reglene er dokumentert og testet.
- **Vanlige feil:** mange regler uten behov; glemme `Enum.IsDefined`.
- **Spørsmål:** Hvilke overganger må kunne angres?
- **Commit:** `feat: møtestatus (MM2)`.

### MM3. Repository-interface, fake og tjeneste
- **Lage:** `IMeetingRepository`, `FakeMeetingRepository`, `MeetingService` (registrer, hent, oppdater, status, slett).
- **Teori:** guide 8, 9, 10; TDD.
- **Les:** `IBoardCaseRepository.cs`, `BoardCaseService.cs`, `FakeBoardCaseRepository.cs`, `BoardCaseServiceTests.cs`.
- **Skriv selv:** interface, tjeneste, fake, `MeetingServiceTests`.
- **Tester:** lagring, `KeyNotFoundException`, feil lagrer ingenting, tid fra `ManualTimeProvider`.
- **Akseptanse:** tjenesten testes uten database.
- **Vanlige feil:** regler i tjenesten som hører i entiteten; glemme `await`.
- **Spørsmål:** Hva er tjenestens jobb og hva er entitetens?
- **Commit:** `feat: møtetjeneste med tester (MM3)`.

### MM4. EF Core, migrering og SQLite-repository
- **Lage:** `DbSet`, `ConfigureMeeting`, `EfMeetingRepository`, migrering `AddMeetings`.
- **Teori:** guide 11, 12.
- **Les:** `SameieHubDbContext.cs`, `EfBoardCaseRepository.cs`, `Migrations/…AddBoardCases.cs`, `BoardCaseMigrationTests`, `BoardCasePersistenceTests`.
- **Skriv selv:** mapping, repository, registrering i `ServiceCollectionExtensions`, migreringen, persistens- og migreringstester.
- **Tester:** rundtur (lagre, les, oppdater, slett), UTC beholdes, oppgradering fra forrige skjema bevarer data, `has-pending-model-changes` er tom.
- **Akseptanse:** alle gamle tester grønne; gamle data uendret etter migrering.
- **Vanlige feil:** glemme `HasConversion` for enum/UTC; `Cascade`; redigere gamle migreringer; mappe navigasjonssamlinger uten avtale.
- **Spørsmål:** Hvorfor `AsNoTracking`? Hva viser snapshotfilen?
- **Commit:** `feat: lagring av møter i SQLite (MM4)`.

### MM5. DI og testoppsett
- **Lage:** registrer `MeetingService` i `WebAppSetup`, `SqliteTestDatabase.BuildProvider`, `UiTestBase` (med fake).
- **Teori:** guide 10.
- **Les:** de tre filene.
- **Skriv selv:** registreringene; oppdater `DatabaseTestFiles.SnapshotAsync` og `DatabaseTestData.FillAllModulesAsync`.
- **Tester:** appen starter; backup- og restoretestene dekker møtetabellen (legg til rad i `FillAllModulesAsync`).
- **Akseptanse:** `dotnet run` mot testdatabase starter uten DI-feil.
- **Vanlige feil:** glemme ett av tre stedene.
- **Spørsmål:** Hva er forskjellen på `AddScoped` og `AddSingleton` her?
- **Commit:** `chore: registrer møtetjenesten og testdata (MM5)`.

### MM6. Liste-side og meny
- **Lage:** `Components/Pages/Meetings/MeetingList.razor` (route avtales, f.eks. `/moter`), statusmerke, tekster; evt. menypunkt.
- **Teori:** guide 13; `UI_DESIGN.md`.
- **Les:** `BoardCaseList.razor`, `PageHeader`, `QuickTabs`, `NavMenu.razor`, `NavigationConsistencyTests`.
- **Skriv selv:** side, `MeetingText`, tester.
- **Tester:** tom tilstand, lister, feil fra repository, lenker; **oppdater bevisst** menytesten som i dag forbyr «møte».
- **Akseptanse:** siden følger designsystemet og virker på smal skjerm.
- **Vanlige feil:** nye UI-mønstre; kun farge som status; glemme `data-testid`.
- **Spørsmål:** Hvor skal menypunktet ligge, og hvorfor?
- **Commit:** `feat: møteoversikt (MM6)`.

### MM7. Opprett og rediger
- **Lage:** `MeetingForm`, `MeetingFormModel`, `MeetingFieldMessages`, `MeetingCreate`, `MeetingEdit`.
- **Teori:** guide 5, 13; sommertid (7) hvis klokkeslett.
- **Les:** `BoardCaseForm.razor` og filene rundt; `ReadingTimeField.razor`.
- **Skriv selv:** skjema og sider.
- **Tester:** bUnit: gyldig lagring og navigasjon, feltfeil fra domenet, dobbel innsending, ukjent id.
- **Akseptanse:** norske meldinger ved feil; ingen verdier logges.
- **Vanlige feil:** validere bare i UI; miste inndata ved feil.
- **Spørsmål:** Hvordan kommer en `ArgumentException` til riktig felt?
- **Commit:** `feat: registrere og redigere møter (MM7)`.

### MM8. Detaljside, status og sletting
- **Lage:** `MeetingDetail` med hovedhandling, «Flere handlinger», slettebekreftelse.
- **Les:** `BoardCaseDetail.razor` (`RunActionAsync`), `ActionFeedback`, `MoreMenu`.
- **Tester:** riktig hovedhandling per status, vern mot dobbeltklikk, slettebekreftelse.
- **Akseptanse:** sletting krever bekreftelse; kan ikke slette noe andre peker på.
- **Vanlige feil:** handlinger uten tilbakemelding.
- **Spørsmål:** Hva er neste naturlige handling for et møte?
- **Commit:** `feat: møtedetaljer og status (MM8)`.

### MM9. Agendapunkter og kobling til styresaker
- **Lage:** `AgendaItem` (egen entitet, `MeetingId`, rekkefølge, valgfri `BoardCaseId`), repository/metoder, migrering, visning og redigering på detaljsiden.
- **Teori:** guide 11, 12; transaksjoner.
- **Les:** `EfChargingRepositories.AddWithMainMeterAsync`, `TaskItemService.EnsureBoardCaseExistsAsync`, `BoardCaseRelationTests`.
- **Skriv selv:** domene → lagring → UI, samme rekkefølge.
- **Tester:** rekkefølge, slettevern (styresak i agenda kan ikke slettes: `Restrict` + `BoardCaseInUseException`), migrering.
- **Akseptanse:** punkter beholder rekkefølge etter omstart; ingen cascade.
- **Vanlige feil:** rekkefølgenumre med hull eller duplikater; glemme at `BoardCaseService.DeleteAsync` nå kan nektes av en ny grunn (oppdater tekst og test).
- **Spørsmål:** Hvorfor egen tabell og ikke en liste i `Meeting`?
- **Commit:** flere: `feat: agendapunkter (MM9a)`, `feat: koble styresaker (MM9b)`.

### MM10. Deltakere, referat, vedtak og oppfølging
- **Lage:** deltakere (etter MM0), referat, vedtak via `BoardCaseService.RegisterDecisionAsync`, «Ny oppgave» fra et punkt (`/oppgaver/ny?styresak={id}`).
- **Les:** `BoardCase.RegisterDecision`, `TaskCreate.razor`.
- **Tester:** vedtaket havner på riktig sak, forhåndsvalg virker, ingen personopplysninger i logg eller testdata.
- **Akseptanse:** ingen duplisert vedtakssannhet (besluttet i MM0).
- **Vanlige feil:** lagre vedtak to steder; logge referattekst.
- **Spørsmål:** Hva er sannheten for et vedtak?
- **Commit:** en per delfunksjon.

### MM11. Integrasjon og avslutning
- **Lage:** oversikt/dashbord, sikkerhetskopisammendrag, demo (valgfritt), dokumentasjon.
- **Les:** `DashboardOverview.cs`, `Dashboard.razor`, `DashboardTests`, `BackupContentSummary.cs`, `BackupPage.razor`, `DemoDatabaseSeeder.cs`.
- **Tester:** dashbordtester oppdatert bevisst; gammel sikkerhetskopi (før møtetabellene) kan gjenopprettes; manuell test mot en egen database; test av oppdatering fra en tidligere installasjon (`Test-Installer.ps1`) hvis det skal distribueres.
- **Akseptanse:** alle tester grønne, CI grønn, dokumentasjonen oppdatert (`ARCHITECTURE`, `UI_DESIGN`, `TESTING`, `REQUIREMENTS`, brukerveiledningen).
- **Vanlige feil:** glemme tekstene som sier at programmet ikke holder styr på møter.
- **Spørsmål:** Hva må en bruker vite om endringen?
- **Commit:** `feat: møter i oversikten og dokumentasjon (MM11)`.

### Anbefalt læringsrekkefølge
Guiden: TDD/AAA → 1–4 → 5–7 (domene) → 8–10 (tjeneste og DI) → 11–12 (lagring) → 13 (Blazor) → 14 (tester).

---

## Del 4. Åpne beslutninger (avklares i MM0)

1. **`BoardCase.MeetingDate` mot møtet:** beholdes, fjernes, eller utledes? Eksisterende datoer må ikke gå tapt ved migrering.
2. **Vedtak:** på styresaken (som i dag), på møtet, eller begge? Hva hvis et vedtak ikke hører til en styresak?
3. **Møtetyper:** bare styremøter, eller også sameiermøte/årsmøte?
4. **Felter:** klokkeslett og sted? Klokkeslett krever sommertidshåndtering.
5. **Statuser** og hvilke overganger som tillates (planlagt, holdt, avlyst, referat godkjent?).
6. **Deltakere:** fritekstnavn (som `TaskItem.AssignedTo`) eller en egen liste over styremedlemmer? Hvilke personopplysninger lagres?
7. **Referat:** ett per møte eller per punkt? Skal det kunne låses etter godkjenning, og må endringshistorikk finnes (nevnt i `REQUIREMENTS.md` for formell protokollføring)?
8. **Agenda:** kan samme styresak stå på flere møter? Kan punkter uten styresak finnes?
9. **Sletting:** hva kan slettes, og hva skal bare kunne avlyses?
10. **Meny og rute:** eget menypunkt (bryter dagens «seks punkter»-regel) eller under Styresaker? Norsk rute (`/moter`)?
11. **Dashbord og sikkerhetskopi:** skal møter vises og telles?
12. **Demo:** fiktive møter i demoen?
13. **Arbeidsregler:** avklart. Eieren skriver koden, MM1–MM11 gjøres på `feature/meeting-management`, og opphav til kode følger 1.10. Gjenstår: hvordan grenen flettes inn i `main` (samlet til slutt eller per milepæl)?
