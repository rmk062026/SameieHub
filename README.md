# SameieHub

Lokalt administrasjonsprogram for små sameier: oversikt over boenheter, styresaker og oppgaver/vedlikehold. Bygget med C#, .NET 10, Blazor (Interactive Server) og SQLite. Kodehode-prosjekt og porteføljeeksempel.

**Status:** M0 fundament ferdig. M1A–M1D ferdig: boenheter kan registreres, vises og få endret adresse i et norsk Blazor-grensesnitt med SQLite-lagring. Eier/kontaktinfo og sletting gjenstår (se [krav](docs/REQUIREMENTS.md)).

![CI](https://github.com/rmk062026/SameieHub/actions/workflows/ci.yml/badge.svg)

## Kom i gang

Krever .NET 10 SDK (kun for utvikling).

```bash
dotnet build SameieHub.slnx
dotnet test SameieHub.slnx
dotnet run --project src/SameieHub.Web
```

## Dokumentasjon
- [Krav](docs/REQUIREMENTS.md)
- [Arkitektur](docs/ARCHITECTURE.md)
- [UI-design](docs/UI_DESIGN.md)
- [Testing](docs/TESTING.md)
- [Utvikling](docs/DEVELOPMENT.md)
- [Prosjektregler for Claude Code](CLAUDE.md)
