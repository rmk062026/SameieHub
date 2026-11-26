# Utvikling

## Krav
Windows, .NET 10 SDK, Git, GitHub CLI (`gh`), Visual Studio 2026 eller annen editor.

## Kommandoer
```bash
dotnet restore SameieHub.slnx
dotnet build SameieHub.slnx
dotnet test SameieHub.slnx
dotnet run --project src/SameieHub.Web
```

## Git-arbeidsflyt
- Repo `rmk062026/SameieHub`, branch `main`; lokal identitet `rmk062026`.
- Før push: `git status`, `git remote -v`, `git config user.name` / `user.email`, `gh api user --jq .login`.
- Conventional Commits; ingen force-push; ingen databasefiler eller backuper i repo.

## Database
- Lokal fil: `%LocalAppData%\SameieHub\sameiehub.db`, opprettet og migrert ved oppstart. Slett filen for å starte med tom database.
- Isolert database for manuell testing: sett miljøvariabelen `Database__Path` til en egen fil, f.eks. i PowerShell `$env:Database__Path = "$env:TEMP\sameiehub-test.db"; dotnet run --project src/SameieHub.Web`.
- Ny migrering: `dotnet tool restore`, deretter `dotnet ef migrations add <Navn> --project src/SameieHub.Infrastructure --output-dir Persistence/Migrations`.

## Pakkeversjoner
EF Core-pakker pinnes til 10.0.x så lenge prosjektet målretter `net10.0`.

## Milepæler
Demodata for visning og manuell testing: se [DEMO.md](DEMO.md) (dobbeltklikk `scripts\Start-demo.cmd`, eller `.\scripts\demo.ps1 create|start|launch|reset|info`).

M0 fundament · M1 Boenheter · M2 Styresaker · M3 Oppgaver · M4 Dashboard · M5 Backup/gjenoppretting og lokal pakking. Hver milepæl avsluttes med kontrollrapport og krever godkjenning.

## Legge til et nytt felt i `HousingUnit`
Eksempel: `ParkingSpace` (tekst). Følg rekkefølgen, og skriv testen først (TDD):
1. **Test (Domain):** i `HousingUnitDetailsTests`, test normalisering og regler for feltet (og at ugyldig verdi ikke endrer noe).
2. **Domenet:** legg til `public string? ParkingSpace { get; private set; }` i `HousingUnit`, en egenskap i `HousingUnitDetails`, en lengdekonstant og en `Normalize…`-linje i `UpdateDetails` (valider først, tilordne sist). Bruk `nameof(...)` som feltnavn i feilen, fordi UI bruker det.
3. **Søk (valgfritt):** legg feltet til i `HousingUnitFilter.MatchesText`.
4. **Database:** legg ved behov mapping i `SameieHubDbContext.OnModelCreating`, og lag migrering: `dotnet ef migrations add AddParkingSpace --project src/SameieHub.Infrastructure --output-dir Persistence/Migrations`. Kontroller den genererte filen, og gi ikke-valgfrie kolonner en fornuftig standardverdi. Endre aldri en migrering som allerede er brukt.
5. **UI:** egenskap i `HousingUnitFormModel` (`From` og `ToDetails`), felt i `HousingUnitForm.razor`, melding i `HousingUnitFieldMessages`, og visning i `HousingUnitDetail.razor` (`DetailItem`).
6. **Tester:** persistenstest og bUnit-test for skjema og detaljside. Kjør `dotnet test`.
7. **Dokumentasjon:** `REQUIREMENTS.md` (regler) og `ARCHITECTURE.md` (beslutning).

## Legge til en tabell som refererer til boenheter
Gjelder `BoardCase` (M2C, gjort) og `TaskItem` (M3C, gjort).
1. Konfigurer fremmednøkkelen med `HasOne<HousingUnit>().WithMany().HasForeignKey(...).OnDelete(DeleteBehavior.Restrict)`. Ikke bruk cascade.
2. Sjekk at den genererte migreringen har `onDelete: ReferentialAction.Restrict`.
3. Skriv tester mot den ekte tabellen etter mønsteret i `BoardCaseRelationTests`: sletting av en boenhet med en tilknyttet post skal gi `HousingUnitInUseException`, og boenheten skal bestå. For en ny tabell som refererer til `BoardCases` (`TaskItem`, M3C, gjort i `TaskItemRelationTests`): tilsvarende test mot `BoardCaseInUseException`.

## Windows-modus (M8-1)
Den vanlige utviklingsstarten er uendret. Windows-modus brukes bare når du ber om den, og er til for å prøve det som skal bli den distribuerte programmen.

```powershell
dotnet run --project src/SameieHub.Web --no-launch-profile -- --windows-host "--Database:Path=$env:TEMP\sameiehub-windows-test.db"
```

- Lytter på `127.0.0.1` med en tilfeldig port (adressen vises i vinduet), åpner nettleseren, og avslutter med Ctrl+C eller lukking av vinduet.
- `--unattended` hindrer at nettleseren åpnes og at meldingsboks vises. Bruk det i skript.
- Loggfiler ligger i `logs` ved siden av databasefilen, og en sikkerhetskopi før oppgradering i `pre-migration`.
- Bruk alltid en egen testdatabase med `--Database:Path`. Demodatabasen avvises i Windows-modus (bruk `Start-demo.cmd`).
- Før M8-3 (engangskode og cookie) er modusen bare for kontrollert testing med fiktive data. Se [WINDOWS_DISTRIBUTION_PLAN.md](WINDOWS_DISTRIBUTION_PLAN.md), vedlegg A.

### Windows-pakken (M8-2)
Den interne testpakken lages og kontrolleres med to skript. Pakken er en selvstendig mappe (`SameieHub.exe` og runtime), og trenger ikke .NET hos den som starter den.

```powershell
.\scripts\Publish-Windows.ps1 -Version 0.8.2-test
```

```powershell
.\scripts\Test-WindowsPackage.ps1 -Zip .\artifacts\windows\SameieHub-0.8.2-test-win-x64-test.zip
```

- Pakken legges i `artifacts\windows` (ignorert av git). Pakk ut zip-filen og dobbeltklikk `SameieHub.exe` (eller `Start-SameieHub.cmd`).
- Den publiserte filen bruker Windows-modus uten flagg. Merket legges bare på ved en self-contained bygging, så `dotnet run` og testene er uendret. `SAMEIEHUB_WINDOWS_HOST=0` slår modusen av for en publisert fil.
- **Pakken bruker databasen i `%LOCALAPPDATA%\SameieHub`, altså den ordinære.** Under testing: sett `Database__Path` til en midlertidig fil, slik røykprøven gjør.
- Røykprøven bruker bare en midlertidig database og kjøres også i CI (jobben `windows-package`). Se [WINDOWS_DISTRIBUTION_PLAN.md](WINDOWS_DISTRIBUTION_PLAN.md), vedlegg B.
