# Demodata: Fiktivdalen Sameie

SameieHub kan startes mot en egen demodatabase med et fullstendig **fiktivt** sameie. Demoen viser hva som skal registreres i hvert felt, og hvordan boenheter, styresaker og oppgaver henger sammen. Den vanlige databasen din blir aldri brukt eller endret.

**Alle data er fiktive.** Navnene er oppdiktede, e-postadressene ligger under `example.com`, og telefonnumrene (`+47 000 00 xxx`) er ugyldige og kan ikke tilhøre noen. Ingen ekte personer, adresser eller ladeboks-ID-er er brukt.

## Kommandoer (Windows, PowerShell)

Kjør fra prosjektmappen. Hvis PowerShell nekter å kjøre skript, bruk `powershell -ExecutionPolicy Bypass -File scripts\demo.ps1 <kommando>`.

| Du vil … | Kommando |
|---|---|
| Opprette demodatabasen | `.\scripts\demo.ps1 create` |
| Starte programmet i demomodus | `.\scripts\demo.ps1 start` (åpne <http://localhost:5100>, avslutt med Ctrl+C) |
| Se hvilke databaser som finnes | `.\scripts\demo.ps1 info` |
| Nullstille og lage demoen på nytt | `.\scripts\demo.ps1 reset` |
| Gå tilbake til den ordinære databasen | Start programmet som vanlig (`dotnet run --project src/SameieHub.Web`, eller Visual Studio). Skriptet setter bare miljøvariabler for sin egen kjøring og rydder opp etterpå. |

`create` kan kjøres flere ganger: finnes demodataene allerede, skjer ingenting. Dagens dato styrer fristene og møtedatoene, så kjør `reset` for å få en demo som passer til datoen du viser den.

## Hvilken database bruker programmet?

- **Demo:** `%LOCALAPPDATA%\SameieHubDemo\sameiehub-demo.db`, port 5100.
- **Ordinær:** `%LOCALAPPDATA%\SameieHub\sameiehub.db` (eller stien i `Database__Path`/`Database:Path`).
- Ved oppstart skriver programmet en linje i konsollen, for eksempel `Using database SameieHubDemo/sameiehub-demo.db`. Bare mappe- og filnavn skrives, siden hele stien kan inneholde Windows-brukernavnet.
- `.\scripts\demo.ps1 info` viser begge stiene.

## Sikkerhet

- Demodata legges **aldri** inn automatisk. Det skjer bare med kommandoen `--seed-demo`, som skriptet kjører.
- Kommandoen krever at `Database__Path` peker på en eksplisitt fil som **ikke** er den ordinære databasen. Ellers avslutter den med feilmelding før noe åpnes.
- Kommandoen fyller bare en **tom** database (eller gjør ingenting hvis demoen alt finnes). En database med andre data blir ikke endret, og kommandoen avslutter med feilmelding.
- Dataene opprettes med de ekte tjenestene (`HousingUnitService`, `BoardCaseService`, `TaskItemService`) og EF Core-migreringene. Det brukes ingen direkte SQL, så alle valideringsregler gjelder.
- Hvis du vil starte demoen på nytt manuelt: slett `%LOCALAPPDATA%\SameieHubDemo\` (bare demomappen) og kjør `create`.

## Innhold

**8 boenheter** (fire tomannsboliger, seksjonsnummer 1–8, 95–145 m², alle aktive): Fiktivdalen 10A/10B, 12A/12B, 14A/14B og 16A/16B. Fire har to eiere, fire har én. Noen har både telefon og e-post, noen bare ett av delene, og 12B har bare navn.

**Carport og elbillader:** hver boenhet har én carport og én installert elbillader. Foreløpig står dette i boenhetens **Merknader**, fordi `HousingUnit` ikke har egne felter for det:

| Boenhet | Elbillader |
|---|---|
| 10A, 12A, 16A, 16B | installert og i bruk |
| 10B, 14B | installert, ingen elbil registrert |
| 12B | installert, men ikke i bruk |
| 14A | installert, men midlertidig deaktivert i påvente av service |

**12 styresaker:** 3 Ny, 4 Under behandling, 3 Vedtatt og 2 Avsluttet. Dekker uteområde, elbilladeanlegget, carporter, fellesbelysning, årlig sameiemøte, gjerde, snørydding, drenering, gjesteparkering, vedlikeholdsplan, lekkasje ved 12B og ladeanleggets kapasitet. Fire har vedtak med vedtaksdato, og én avsluttet sak har vedtak uten dato. Møtedatoene ligger både bakover og fremover i tid. Én sak er knyttet til en boenhet (12B), resten gjelder hele sameiet.

**20 oppgaver:** 11 Ikke startet, 4 Pågår og 5 Fullført (med fullføringstidspunkt). Fire er forfalt, to har frist i dag, tre har frist innen sju dager, tre har frist om 35–55 dager, og to har ingen frist. Både generelle oppgaver og vedlikehold, alle tre prioriteter, med og uten ansvarlig, og med alle kombinasjoner av koblinger: bare boenhet, bare styresak, begge (14A og 12B) og ingen.

Tidspunktene for opprettelse og fullføring er spredt over de siste månedene, men aldri frem i tid.

## Mulig senere funksjonalitet (utenfor v1.0, ikke laget)

Carport og elbillading ligger i merknader inntil videre. Mulige utvidelser senere:

- Egne felter for carport (plassnummer, beskrivelse).
- Laderens modell og produsent.
- Laderens status (aktiv/deaktivert) som eget felt.
- Tilknytning av en lader til en boenhet som egen modell.
- Integrasjon med et eksternt ladesystem.
- Strømforbruk og ladehistorikk.

Dette krever en egen avgjørelse og migrering, og er ikke en del av gjeldende plan.
