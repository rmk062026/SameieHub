# Dokumentasjon

Indeks over dokumentene i `docs/`. Den korte oversikten over prosjektet står i [../README.md](../README.md), og arbeidsreglene for AI-agenten i [../CLAUDE.md](../CLAUDE.md).

## Anbefalt leserekkefølge (for å forstå og kunne presentere prosjektet)

| # | Dokument | Les det for å … |
|---|---|---|
| 1 | [../README.md](../README.md) | få det raske overblikket og status |
| 2 | [PROJECT_STRUCTURE.md](PROJECT_STRUCTURE.md) | vite hva som ligger hvor, og hvorfor det er fire prosjekter |
| 3 | [ARCHITECTURE_EXPLAINED.md](ARCHITECTURE_EXPLAINED.md) | forstå hvordan delene samarbeider, fra klikk til database |
| 4 | [CODE_WALKTHROUGH.md](CODE_WALKTHROUGH.md) | lese den faktiske C#-koden linje for linje |
| 5 | [DESIGN_DECISIONS.md](DESIGN_DECISIONS.md) | kunne forklare *hvorfor* valgene ble tatt, med alternativer |
| 6 | [DEMO.md](DEMO.md) | kjøre demoen med fiktive data |
| 7 | [MY_CONTRIBUTIONS.md](MY_CONTRIBUTIONS.md) | gjennomgå prosjektfortellingen (roller, arkitekturbidrag og læringsmål) og kontrollere at den stemmer med egne erfaringer |
| 8 | [PRESENTATION_GUIDE.md](PRESENTATION_GUIDE.md) | forberede presentasjon og øve på spørsmål |

## Prosjektdokumentasjon som er skrevet underveis (historisk)

Disse ble skrevet i samme endring som koden, milepæl for milepæl, og beskriver hva som ble gjort og hvorfor *da*. De er ikke omskrevet i ettertid.

| Dokument | Innhold |
|---|---|
| [REQUIREMENTS.md](REQUIREMENTS.md) | Krav, regler per område og avgrensninger |
| [ARCHITECTURE.md](ARCHITECTURE.md) | Arkitektur, beslutninger og regler per milepæl (M1–M4E-A) |
| [UI_DESIGN.md](UI_DESIGN.md) | Sider, ruter og designregler |
| [TESTING.md](TESTING.md) | Teststrategi og hva testene dekker |
| [DEVELOPMENT.md](DEVELOPMENT.md) | Kommandoer, database, git-arbeidsflyt, hvordan legge til et felt |
| [DEMO.md](DEMO.md) | Demodatabasen, kommandoer og sikkerhetsregler |
| [BRUKERVEILEDNING.md](BRUKERVEILEDNING.md) | Installere, bruke, sikkerhetskopiere, oppdatere og avinstallere SameieHub (for brukere) |
| [RELEASE_PROCESS.md](RELEASE_PROCESS.md) | Hvordan en utgivelse bygges, testes og godkjennes |
| [RELEASE_CHECKLIST.md](RELEASE_CHECKLIST.md) | Status per utgivelseskriterium: bestått, ikke bestått, ikke testet |
| [WINDOWS_ACCEPTANCE_REPORT.md](WINDOWS_ACCEPTANCE_REPORT.md) | Akseptansekontroll av Windows-distribusjonen: hva som er prøvd, ikke prøvd, sesjonsproblemet og oppskrift for ren maskin |
| [TEST_AUDIT.md](TEST_AUDIT.md) | Revisjon av testpakken og plan for forbedring (M4T) |
| [REDESIGN_PLAN.md](REDESIGN_PLAN.md) | Plan for å forenkle brukeropplevelsen (M5D, ikke implementert) |
| [WINDOWS_DISTRIBUTION_PLAN.md](WINDOWS_DISTRIBUTION_PLAN.md) | Plan for selvstendig Windows-versjon: arkitekturvalg, oppstart og avslutning, sikkerhet, lagring, installasjon og milepæler (M8, bare plan) |
| [USABILITY_REVIEW.md](USABILITY_REVIEW.md) | Brukervennlighet, antakelser om det første sameiet og anbefalinger (M4T-P1.2) |

## Møteadministrasjon: forberedelse (studenten skriver koden, ingen møtekode finnes)

| Dokument | Innhold |
|---|---|
| [MEETING_MODULE_LEARNING_GUIDE.md](MEETING_MODULE_LEARNING_GUIDE.md) | Læringsguide: C#/.NET-konseptene med eksempler fra koden, øvelser og kontrollspørsmål |
| [MEETING_MODULE_ARCHITECTURE.md](MEETING_MODULE_ARCHITECTURE.md) | Faktisk arkitektur og dataflyt, og hvordan en ny modul kobles inn |
| [MEETING_MODULE_IMPLEMENTATION_PLAN.md](MEETING_MODULE_IMPLEMENTATION_PLAN.md) | Utviklingsrutine, foreslått faglig struktur, milepæler og åpne beslutninger |
| [MEETING_MODULE_REFERENCE.md](MEETING_MODULE_REFERENCE.md) | Fil-for-fil-oversikt over eksisterende kode som forbilde |

## Status (kontrollert mot koden)

- Ferdig gjennom alle lag: boenheter, styresaker, oppgaver, dashboard, demodata.
- Ferdig gjennom alle lag: elbillading (domene M4E-A, lagring M4E-B, sider M4E-C, demodata M4E-D).
- Ikke laget: backup/gjenoppretting og pakking (M5).
