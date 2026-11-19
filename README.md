# SameieHub

Lokalt administrasjonsprogram for små sameier: oversikt over boenheter, styresaker og oppgaver/vedlikehold. Bygget med C#, .NET 10, Blazor (Interactive Server) og SQLite. Kodehode-prosjekt og porteføljeeksempel.

**Status:** M0–M1E ferdig: boenhetsregisteret (registrere, vise, redigere, søke, deaktivere og slette (M1F), med opptil to eiere per boenhet) i et norsk Blazor-grensesnitt med SQLite-lagring. Styresaker (M2): registrere, redigere, søke, filtrere, endre status, registrere vedtak (med vedtaksdato) og slette, med kobling til boenheter og snarvei fra boenhetens side. Dashboard er ikke laget ennå. Oppgaver og vedlikehold (M3): domenemodell, tjeneste, SQLite-lagring med fremmednøkler til boenheter og styresaker, og et komplett norsk grensesnitt med oversikt, søk, filtre, sortering, forfalt/frist i dag, statusendring, kobling til boenheter og styresaker og sletting (M3D).

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
