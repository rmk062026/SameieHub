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
- Ny migrering: `dotnet tool restore`, deretter `dotnet ef migrations add <Navn> --project src/SameieHub.Infrastructure --output-dir Persistence/Migrations`.

## Pakkeversjoner
EF Core-pakker pinnes til 10.0.x så lenge prosjektet målretter `net10.0`.

## Milepæler
M0 fundament · M1 Boenheter · M2 Styresaker · M3 Oppgaver · M4 Dashboard · M5 Backup/gjenoppretting og lokal pakking. Hver milepæl avsluttes med kontrollrapport og krever godkjenning.
