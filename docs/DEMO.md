# Demodata: Fiktivdalen Sameie

SameieHub kan startes mot en egen demodatabase med et fullstendig **fiktivt** sameie. Demoen viser hva som skal registreres i hvert felt, hvordan boenheter, styresaker og oppgaver henger sammen, og hvordan elbillading med hovedmåler, undermålere og avstemming fungerer. Den vanlige databasen din blir aldri brukt eller endret.

**Alle data er fiktive.** Navnene er oppdiktede, e-postadressene ligger under `example.com`, og telefonnumrene (`+47 000 00 xxx`) er ugyldige og kan ikke tilhøre noen. Ingen ekte personer, adresser, serienumre, brukerkontoer eller API-nøkler er brukt.

## Start demoen med dobbeltklikk

Dobbeltklikk **`scripts\Start-demo.cmd`** i Utforsker. Filen:

1. kjører `scripts\demo.ps1 launch` i en PowerShell-prosess med `-ExecutionPolicy Bypass`. Det gjelder bare denne ene prosessen; ingen varig sikkerhetsinnstilling endres, og ingenting installeres;
2. oppretter demodatabasen hvis den mangler (gjennom den vanlige, sikre seed-kommandoen);
3. starter programmet mot demodatabasen på port 5100;
4. åpner <http://localhost:5100> i nettleseren **først når serveren faktisk svarer**;
5. holder vinduet åpent mens demoen kjører. Lukk vinduet eller trykk Ctrl+C for å stoppe.

Feil vises i vinduet med en tydelig melding, og vinduet blir stående til du trykker en tast:

| Situasjon | Melding |
|---|---|
| `dotnet` mangler | «The .NET SDK (the dotnet command) was not found …» |
| Port 5100 er i bruk | «Port 5100 is already in use. Is the demo already running? …» (ingenting startes) |
| Programmet stopper under oppstart | «SameieHub stopped during startup (exit code …). See the message above.» |
| Ingen svar innen 3 minutter | «SameieHub did not answer within 3 minutes …» |

Dette er oppstarten for utviklingsdemoen og krever .NET SDK. Den ferdige selvstendige Windows-pakken kommer i M5C.

**Nullstille:** dobbeltklikk **`scripts\Nullstill-demo.cmd`**. Den spør først (J/N), sletter bare demodatabasen og lager den på nytt. Stopp demoen først.

## Kommandoer (Windows, PowerShell)

Kjør fra prosjektmappen. Hvis PowerShell nekter å kjøre skript, bruk `powershell -ExecutionPolicy Bypass -File scripts\demo.ps1 <kommando>`.

| Du vil … | Kommando |
|---|---|
| Opprette demodatabasen manuelt | `.\scripts\demo.ps1 create` |
| Starte programmet i demomodus (uten å åpne nettleser) | `.\scripts\demo.ps1 start` (åpne <http://localhost:5100>, avslutt med Ctrl+C) |
| Opprette ved behov, starte og åpne nettleseren | `.\scripts\demo.ps1 launch` (det samme som dobbeltklikk) |
| Se hvilke databaser som finnes | `.\scripts\demo.ps1 info` |
| Nullstille og lage demoen på nytt | `.\scripts\demo.ps1 reset` |
| Gå tilbake til den ordinære databasen | Start programmet som vanlig (`dotnet run --project src/SameieHub.Web`, eller Visual Studio). Skriptet setter bare miljøvariabler for sin egen kjøring og rydder opp etterpå. |

`create` kan kjøres flere ganger: er demoen komplett, skjer ingenting. Dagens dato styrer fristene, møtedatoene og avlesningstidspunktene, så kjør `reset` for å få en demo som passer til datoen du viser den.

## Hvilken database bruker programmet?

- **Demo:** `%LOCALAPPDATA%\SameieHubDemo\sameiehub-demo.db`, port 5100.
- **Ordinær:** `%LOCALAPPDATA%\SameieHub\sameiehub.db` (eller stien i `Database__Path`/`Database:Path`).
- Ved oppstart skriver programmet en linje i konsollen, for eksempel `Using database SameieHubDemo/sameiehub-demo.db`. Bare mappe- og filnavn skrives, siden hele stien kan inneholde Windows-brukernavnet.
- `.\scripts\demo.ps1 info` viser begge stiene.

## Eldre demoer

Tidligere versjoner av demoen (før M4E-D) har åtte boenheter, tolv styresaker og tjue oppgaver, men **ingen ladedata**. Slike demoer håndteres slik:

- **Ingen automatisk oppdatering.** Normal oppstart og `create` legger aldri ladedata inn i en eksisterende demo. Dine egne endringer i demoen overskrives aldri.
- **Klar beskjed.** `start` og `launch` sjekker demoen (`--check-demo`, som ikke endrer noe) og skriver en advarsel: «The existing demo is older and has no charging facility …». Demoen startes likevel som den er. `create` gir feilmeldingen «The demo database is an older demo without the charging facility. Nothing was changed, and your changes are kept …».
- **Bevisst nyoppretting.** Kjør `reset` (eller dobbeltklikk `Nullstill-demo.cmd`). Det sletter demodatabasen og lager den nye demoen. Endringer du har gjort i den eldre demoen går tapt, så ta en kopi av filen først hvis du vil beholde dem.
- Ved oppstart oppgraderer programmet skjemaet til siste migrering (nye tomme tabeller for elbillading). Det endrer ikke eksisterende rader.

## Sikkerhet

- Demodata legges **aldri** inn automatisk. Det skjer bare med kommandoen `--seed-demo`, som skriptet kjører.
- Kommandoen kjører **bare** mot den dedikerte demodatabasen `%LOCALAPPDATA%\SameieHubDemo\sameiehub-demo.db`. Stien i `Database__Path` gjøres om til en absolutt, normalisert sti (`..`, `.` og store/små bokstaver spiller ingen rolle) og sammenlignes med demostien. Den ordinære databasen, andre filer og manglende sti avvises med en melding før noe åpnes eller opprettes. Det samme gjelder `--check-demo`. Vanlig programstart bruker fortsatt den ordinære databasen eller `Database__Path`, uendret.
- Seed-kommandoen skiller mellom disse tilstandene (`DemoDatabaseState`):

| Databasen inneholder | Resultat |
|---|---|
| ingenting | Demoen opprettes. |
| hele demoen (boenheter, styresaker og oppgaver med riktige antall og kontrollpunkter, og ladeanlegget med riktige ladere, skap og målerstander) | Ingenting skjer: «The demo data is already in the database.» |
| en eldre demo uten ladedata | Feilmelding (`DemoDatabaseOutdatedException`). Ingenting endres. Kjør `reset`. |
| en del av demoen, eller en demo der noe er fjernet eller endret (for eksempel en slettet eller korrigert demoavlesning) | Feilmelding om delvis opprettet demo. Ingenting endres. Kjør `reset`. |
| noe annet (egne data, andre antall) | Feilmelding. Ingenting endres. |

- **Fullstendig betyr riktig innhold, ikke bare riktig antall rader.** Kontrollen sjekker adressene, den siste styresaken og oppgaven, ladeanleggets navn, laderne per boenhet, sikringsskap og de planlagte målerstandene for hver måler. Avlesninger du registrerer **etter** at demoen ble laget påvirker ikke kontrollen. Det er en bevisst avgjørelse at det ikke finnes en egen versjonsmarkør i databasen: en markør kan påstå at demoen er ferdig selv om data mangler, mens innholdskontrollen ikke kan det, og den krever ikke nytt skjema.
- Rekkefølgen er boenheter, styresaker, oppgaver og til slutt ladeanlegget. Feiler opprettelsen i de tre første delene, fjernes alt som er opprettet, slik at databasen blir tom. Feiler den under opprettelsen av ladedata, står demoen igjen som delvis opprettet og meldingen sier at du skal kjøre `reset`. Ladere, målere og avlesninger har med vilje ingen sletting i programmet, så det finnes ingen slettelogikk for dem.
- Dataene opprettes med de ekte tjenestene (`HousingUnitService`, `BoardCaseService`, `TaskItemService`, `ChargingService`) og EF Core-migreringene. Det brukes ingen direkte SQL, så alle valideringsregler gjelder, også kronologireglene for målerstander.
- **`reset` sletter bare demodatabasen:** `sameiehub-demo.db` og de tilhørende `-shm`-, `-wal`- og `-journal`-filene i `SameieHubDemo`-mappen, aldri mappen selv og aldri noe annet. Siden hele filen slettes, er det ingen fremmednøkler eller tabeller å rydde i. Skriptet kontrollerer først at stien er demostien og ikke den ordinære. Kjører demoen fortsatt (porten 5100 er i bruk eller filen er åpen), avbryter `reset` med meldingen «The demo database is in use. Stop the demo …» uten å slette noe.

## Innhold

**8 boenheter** (fire tomannsboliger, seksjonsnummer 1–8, 95–145 m², alle aktive): Fiktivdalen 10A/10B, 12A/12B, 14A/14B og 16A/16B. Fire har to eiere, fire har én. Noen har både telefon og e-post, noen bare ett av delene, og 12B har bare navn. Merknadene beskriver bare carporten; elbilladeren er en egen modell (se under), så informasjonen ikke blir motstridende.

**12 styresaker:** 3 Ny, 4 Under behandling, 3 Vedtatt og 2 Avsluttet. Dekker uteområde, elbilladeanlegget, carporter, fellesbelysning, årlig sameiemøte, gjerde, snørydding, drenering, gjesteparkering, vedlikeholdsplan, lekkasje ved 12B og ladeanleggets kapasitet. Fire har vedtak med vedtaksdato, og én avsluttet sak har vedtak uten dato. Møtedatoene ligger både bakover og fremover i tid. Én sak er knyttet til en boenhet (12B), resten gjelder hele sameiet.

**20 oppgaver:** 11 Ikke startet, 4 Pågår og 5 Fullført (med fullføringstidspunkt). Fire er forfalt, to har frist i dag, tre har frist innen sju dager, tre har frist om 35–55 dager, og to har ingen frist. Både generelle oppgaver og vedlikehold, alle tre prioriteter, med og uten ansvarlig, og med alle kombinasjoner av koblinger.

Tidspunktene for opprettelse og fullføring er spredt over de siste månedene, men aldri frem i tid.

### Elbillading: ladeanlegget «Fiktivdalen ladeanlegg»

Ett felles anlegg med **én hovedmåler**, **åtte ladere** (én per boenhet) og **åtte undermålere**, fire i hvert av to sikringsskap. Fordelingen på skap gjelder bare demoen; programmet har ingen slik fast regel.

| Boenhet | Lader | Skap | Status | Elbil | Merknad |
|---|---|---|---|---|---|
| 10A | L10A | 1 | I bruk | ja | |
| 10B | L10B | 1 | Ikke i bruk | nei | null forbruk |
| 12A | L12A | 1 | I bruk | ja | |
| 12B | L12B | 1 | Ikke i bruk | nei | startverdi 0 kWh |
| 14A | L14A | 2 | **Midlertidig deaktivert** | ja | historisk forbruk er bevart |
| 14B | L14B | 2 | Ikke i bruk | nei | én avlesning mangler |
| 16A | L16A | 2 | I bruk | ja | avlesning komplettert senere |
| 16B | L16B | 2 | I bruk | ja | |

Fire ladere er i bruk, tre ikke i bruk og én midlertidig deaktivert. Produsenten er den oppdiktede «Fiktiv Ladeteknikk AS» (modellene FL-7 og FL-11). Statusen er administrativ og stopper ikke avlesninger.

**5 registreringsrunder, 44 avlesninger.** Alle målerstander er akkumulerte (kWh-telleren). Forbruk i en periode er differansen mellom to avlesninger og lagres aldri. Rundene er kl. 08:00 norsk tid, 110, 80, 50, 25 og 4 dager før demoen ble opprettet:

| Runde | Innhold |
|---|---|
| 1 | Fullstendig. Første avlesning er startverdi for alle ni målere. |
| 2 | **Ufullstendig:** 14B ble ikke avlest, og 16A ble ikke avlest samtidig. 16A ble **komplettert dagen etter** med samme avlesningstidspunkt (senere komplettering). 14B mangler for alltid. |
| 3, 4, 5 | Fullstendige. |

**Avstemming** (differanse = hovedmålerens forbruk − summen av undermålernes forbruk; prosent = differanse / hovedmålerens forbruk):

| Periode | Hovedmåler | Undermålere | Differanse | Resultat |
|---|---|---|---|---|
| 1 (runde 1→2) | 1 099 kWh | 1 080 kWh (bare dokumenterte) | ikke beregnet | **Ufullstendig:** 14B mangler avlesning ved slutt |
| 2 (runde 2→3) | 1 154 kWh | 1 135 kWh (bare dokumenterte) | ikke beregnet | **Ufullstendig:** 14B mangler avlesning ved start |
| 3 (runde 3→4) | 1 250 kWh | 1 220 kWh | **+30 kWh (2,4 %)** | Fullstendig |
| 4 (runde 4→5) | 1 080 kWh | 1 065 kWh | +15 kWh (1,4 %) | Fullstendig (den siste, vist på dashboardet) |

Differansen sier bare hvor mye hovedmåleren målte utover undermålerne. Den er **ikke** nødvendigvis energitap: fellesbelysning, målenøyaktighet og avlesningstidspunkt kan også bidra. 14A (deaktivert) har 400 kWh historisk forbruk i periode 1–3 og 0 kWh i periode 4, og avlesningene er bevart. Laderne uten elbil har 0 kWh dokumentert forbruk.

## Prøv de viktigste funksjonene

1. **Dashboard:** seksjonen «Elbillading» viser 8 ladere, 4 i bruk og siste avstemming (+15 kWh, +1,4 %).
2. **Elbillading:** hovedmåler, tall per status, og en tabell med alle åtte ladere, skap og siste avlesning.
3. **Avstemming:** velg perioden i listen. Periode 3 viser 1 250 / 1 220 kWh og +30 kWh (+2,4 %). Periode 1 og 2 viser hva som mangler og at differansen ikke er beregnet.
4. **Historikk:** åpne «Avlesningshistorikk» på hovedmåleren, og lader 14A (deaktivert, men med historikk).
5. **Ny avlesning:** «Registrer avlesning». Feltene viser forrige målerstand. Skriv en lavere verdi enn forrige for å se feilmeldingen ved feltet. Tomt felt lagres ikke.
6. **Korrigering:** i historikken, velg «Korriger» på en avlesning og endre målerstanden. Verdien må passe mellom naboavlesningene.
7. **Boenhet:** fra laderen, «Gå til boenheten». Boenhetssiden har en kompakt elbillader-seksjon.
8. **Smal skjerm:** gjør nettleservinduet smalt (375 px).

Etter at du har endret demoen kan du alltid nullstille den (se over).

## Mulig senere funksjonalitet (utenfor v1.0, ikke laget)

- Egne felter for carport (plassnummer, beskrivelse).
- Integrasjon med et eksternt ladesystem, automatisk avlesning, fjernstyring av lader og fakturering av ladestrøm.
- Sletting av ladere og målerhistorikk i programmet (krever en egen avgjørelse).
