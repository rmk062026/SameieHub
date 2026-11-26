# SameieHub

Lokalt administrasjonsprogram for små sameier: oversikt over boenheter, styresaker og oppgaver/vedlikehold. Bygget med C#, .NET 10, Blazor (Interactive Server) og SQLite. Kodehode-prosjekt og porteføljeeksempel.

**Status:** M0–M1E ferdig: boenhetsregisteret (registrere, vise, redigere, søke, deaktivere og slette (M1F), med opptil to eiere per boenhet) i et norsk Blazor-grensesnitt med SQLite-lagring. Styresaker (M2, redesignet i M5D-R3): registrere, redigere, søke, filtrere med hurtigvalg, endre status, registrere vedtak (med vedtaksdato) og slette, med kobling til boenheter og snarvei fra boenhetens side. Oversikt (M4, redesignet i M5D-R1): tre tall, «Må følges opp», kommende saker med møtedato, elbilstatus og «Kom i gang» for nye sameier. Elbillading (M4E-C): ladeanlegg med felles hovedmåler og åtte undermålere (to sikringsskap), ladere per boenhet, samlet manuell avlesning med norsk tid, avstemming per periode med toleranse, historikk med korrigering og sletting, og seksjoner på boenhetssiden og dashboardet og demodata med komplett ladeanlegg (M4E-D). Oppgaver og vedlikehold (M3): domenemodell, tjeneste, SQLite-lagring med fremmednøkler til boenheter og styresaker, og et komplett norsk grensesnitt med oversikt, søk, filtre, sortering, forfalt/frist i dag, statusendring, kobling til boenheter og styresaker og sletting (M3D).

![CI](https://github.com/rmk062026/SameieHub/actions/workflows/ci.yml/badge.svg)

## Demo

En fiktiv demodatabase (Fiktivdalen Sameie, med ladeanlegg og avstemminger) viser programmet med realistiske data. Dobbeltklikk `scripts\Start-demo.cmd` (oppretter demoen ved behov og åpner nettleseren), eller bruk `.\scripts\demo.ps1 create` og `.\scripts\demo.ps1 start`. Den vanlige databasen berøres aldri. Se [docs/DEMO.md](docs/DEMO.md).

## Kom i gang

Krever .NET 10 SDK (kun for utvikling).

```bash
dotnet build SameieHub.slnx
dotnet test SameieHub.slnx
dotnet run --project src/SameieHub.Web
```

## Windows-testpakke (intern)

`.\scripts\Publish-Windows.ps1` lager en selvstendig mappe og zip (`SameieHub.exe`, ingen .NET nødvendig) for intern testing. Den har ingen tilgangsbeskyttelse ennå og skal ikke distribueres eller brukes med personopplysninger. Se [docs/WINDOWS_DISTRIBUTION_PLAN.md](docs/WINDOWS_DISTRIBUTION_PLAN.md).

## Dokumentasjon
Se [docs/README.md](docs/README.md) for indeks og anbefalt leserekkefølge. Forklaringer for å forstå og presentere prosjektet: [Prosjektstruktur](docs/PROJECT_STRUCTURE.md), [Arkitekturen forklart](docs/ARCHITECTURE_EXPLAINED.md), [Kodegjennomgang](docs/CODE_WALKTHROUGH.md), [Designbeslutninger](docs/DESIGN_DECISIONS.md), [Mine bidrag](docs/MY_CONTRIBUTIONS.md) og [Presentasjonsguide](docs/PRESENTATION_GUIDE.md).

- [Krav](docs/REQUIREMENTS.md)
- [Arkitektur](docs/ARCHITECTURE.md)
- [UI-design](docs/UI_DESIGN.md)
- [Testing](docs/TESTING.md)
- [Utvikling](docs/DEVELOPMENT.md)
- [Prosjektregler for Claude Code](CLAUDE.md)
