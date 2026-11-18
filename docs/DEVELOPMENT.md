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
Gjelder `BoardCase` (M2C) og `TaskItem` (M3).
1. Konfigurer fremmednøkkelen med `HasOne<HousingUnit>().WithMany().HasForeignKey(...).OnDelete(DeleteBehavior.Restrict)`. Ikke bruk cascade.
2. Sjekk at den genererte migreringen har `onDelete: ReferentialAction.Restrict`.
3. Erstatt de simulerte testene (`TestReferences`) i `HousingUnitDeletionTests` med tester mot den ekte tabellen: sletting av en boenhet med en tilknyttet post skal gi `HousingUnitInUseException`, og boenheten skal bestå.
