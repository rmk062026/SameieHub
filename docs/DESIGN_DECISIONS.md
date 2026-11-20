# Designbeslutninger

Dette dokumentet samler de viktigste beslutningene i SameieHub. For hver beslutning står det:

1. **Valg:** hva vi valgte.
2. **Problem:** hva valget skulle løse.
3. **Fordeler.**
4. **Ulemper og begrensninger.**
5. **Alternativer** vi kunne valgt i stedet.

**Hvordan kildene brukes.** Der repoet allerede har en skrevet begrunnelse, er den gjengitt og merket *«Dokumentert»* med kilde (for eksempel tabellen «Beslutninger» i [ARCHITECTURE.md](ARCHITECTURE.md) eller [CLAUDE.md](../CLAUDE.md)). Der repoet ikke sier *hvorfor* en beslutning ble tatt, står det *«Ikke dokumentert»*, og vurderingene som følger er faglige resonnementer, **ikke** påstander om prosjekteierens egne motiver. Punkter som prosjekteieren bør bekrefte, er merket **«Må bekreftes av prosjekteier»**. Det historiske beslutningsmaterialet i `ARCHITECTURE.md` og de andre dokumentene er ikke endret; dette dokumentet er en samlet oversikt skrevet etterpå.

Se også [ARCHITECTURE_EXPLAINED.md](ARCHITECTURE_EXPLAINED.md) (hvordan det virker) og [MY_CONTRIBUTIONS.md](MY_CONTRIBUTIONS.md) (hvem som gjorde hva).

## Innhold
1. [C# og .NET 10](#1-c-og-net-10)
2. [Blazor Interactive Server fremfor WPF](#2-blazor-interactive-server-fremfor-wpf)
3. [SQLite og EF Core](#3-sqlite-og-ef-core)
4. [Lokal applikasjon](#4-lokal-applikasjon)
5. [Separate Domain-, Infrastructure- og Web-prosjekter](#5-separate-domain--infrastructure--og-web-prosjekter)
6. [Repositories og interfaces](#6-repositories-og-interfaces)
7. [`IDbContextFactory` med kortlevd kontekst](#7-idbcontextfactory-med-kortlevd-kontekst)
8. [Fremmednøkler og `RESTRICT`](#8-fremmednøkler-og-restrict)
9. [Egne domeneregler i entitetene](#9-egne-domeneregler-i-entitetene)
10. [UTC for tidspunkt og norsk kalenderdato](#10-utc-for-tidspunkt-og-norsk-kalenderdato)
11. [Automatiserte tester](#11-automatiserte-tester)
12. [Utvikling i milepæler](#12-utvikling-i-milepæler)
13. [Demoen er isolert fra ordinære data](#13-demoen-er-isolert-fra-ordinære-data)
14. [Elbillading: modellvalg (M4E-A)](#14-elbillading-modellvalg-m4e-a)
15. [Funksjoner som er utsatt](#15-funksjoner-som-er-utsatt)
16. [AI-agenten implementerer, eieren godkjenner](#16-ai-agenten-implementerer-eieren-godkjenner)

---

## 1. C# og .NET 10

- **Valg:** C# på .NET 10.
- **Dokumentert:** teknologistakken står i `CLAUDE.md` («C#, .NET 10, Blazor Web App (Interactive Server), SQLite + EF Core, xUnit, GitHub Actions. Windows lokalt») og i milepælsbeskrivelsen M0 som prosjekteieren ga. `CLAUDE.md` sier også at prosjektet er en Kodehode-innlevering og at eieren lærer C#/.NET.
- **Problem:** Prosjektet skal være en innlevering i et C#/.NET-kurs og samtidig være et brukbart program.
- **Fordeler:** Sterk typing og nullable-kontroll fanger mange feil ved bygging. Hele stakken (språk, web, database, test) er fra samme økosystem. .NET 10 er den versjonen prosjektet ble startet på.
- **Ulemper:** Programmet trenger .NET-kjøretid for å kjøre. «Må kunne kjøres uten Visual Studio, .NET SDK og internett» (CLAUDE.md) krever selvstendig publisering, som er planlagt i M5 og **ikke** gjort ennå.
- **Alternativer:** Python/Flask, JavaScript/Node eller Java. **Ikke dokumentert** om disse ble vurdert.
- **Må bekreftes av prosjekteier:** om C#/.NET var et krav i Kodehode-oppgaven eller et eget valg.

## 2. Blazor Interactive Server fremfor WPF

- **Valg:** Blazor Web App med Interactive Server.
- **Dokumentert** (`ARCHITECTURE.md`, tabellen «Beslutninger»): alternativene var «WebAssembly, MAUI/WPF», og begrunnelsen er «Én lokal prosess, ingen API, enklest å pakke».
- **Problem:** Et grafisk grensesnitt for et lokalt program, uten å bygge et eget API.
- **Fordeler:**
  - Komponentene kjører på serveren og kan bruke tjenestene og databasen direkte (`@inject`). Det trengs ingen REST-API eller serialisering.
  - Nettleseren gir responsivt oppsett (testet ved 375 px) uten ekstra arbeid.
  - Blazor-komponenter kan testes uten nettleser med bUnit.
- **Ulemper:**
  - Programmet starter en webserver og brukeren åpner nettleseren, noe som føles mindre «som et ekte program» enn WPF.
  - Interactive Server krever at forbindelsen holdes åpen (SignalR). Brytes den, vises `ReconnectModal`.
  - Det blir en kobling mellom nettleserfane og server (en «krets») som man må forstå (DI-levetider).
- **Alternativer:** WPF (skrivebordsapp, ingen webserver, men kun Windows og mer XAML), Blazor WebAssembly (kjører i nettleseren, men trenger API for databasen), MAUI.
- **Ikke dokumentert:** at WPF ble vurdert som en *første versjon*-kandidat utover raden i tabellen. **Må bekreftes av prosjekteier** hvis dette skal presenteres som en bevisst avveining.

## 3. SQLite og EF Core

- **Valg:** SQLite som database og EF Core som O/R-mapper (pinnet til 10.0.15).
- **Dokumentert** (`ARCHITECTURE.md`): alternativer «JSON-filer, SQL Server»; begrunnelse «Én fil å sikkerhetskopiere, ingen server, relasjoner og migreringer». EF Core 10.0.x fordi «pakkene må matche `net10.0`» (EF Core 11 krever net11).
- **Problem:** Varig lagring av relaterte data (boenheter, saker, oppgaver) i et lokalt program uten at brukeren skal installere en databaseserver.
- **Fordeler:** Én fil i `%LOCALAPPDATA%`, enkel backup (planlagt i M5), ekte fremmednøkler og migreringer, og SQL-kunnskap som kan gjenbrukes.
- **Ulemper og begrensninger (målt eller dokumentert i repoet):**
  - SQLite håndhever ikke fremmednøkler uten `Foreign Keys=True` i tilkoblingsstrengen.
  - `DateTime` lagres som tekst uten tidssone og leses tilbake som `Unspecified`, så en konverter trengs (se punkt 10).
  - `decimal` lagres som tekst, så vi sorterer og summerer ikke på dem i databasen.
  - SQLite håndhever ikke tekstlengder; grensene ligger i domenet.
  - Bare én bruker/prosess om gangen er realistisk.
  - SQLite sin feilkode for `RESTRICT` ved sletting (1811) deles med alle triggere. Det krevde ekstra klassifisering (`SqliteConstraintErrors`).
- **Alternativer:** JSON-filer (enklest, men ingen relasjoner), SQL Server (kraftigere, men krever server), Dapper/rå SQL i stedet for EF Core (mer kontroll, men mer kode og ingen migreringer ut av boksen).

## 4. Lokal applikasjon

- **Valg:** Ett lokalt program for én bruker. Ingen innlogging, ingen skyhosting, ingen Docker, ingen separat API.
- **Dokumentert:** `CLAUDE.md` (avgrensninger) og `REQUIREMENTS.md` («Utenfor v1.0: … flere brukere/innlogging … ekstern tilgang … flere sameier …»). Personopplysninger (eiernavn, telefon, e-post) lagres bare i den lokale databasen.
- **Problem:** Sameiet er lite, og dataene inneholder personopplysninger. Programmet skal være enkelt å drifte.
- **Fordeler:** Færre sikkerhetsflater (ingen autentisering å lekke), ingen drift, data forlater ikke PC-en.
- **Ulemper:** Ingen tilgang fra flere enheter eller flere styremedlemmer samtidig. Backup er brukerens ansvar (M5 skal gi en funksjon for det, ikke laget ennå). Ingen revisjonslogg for hvem som endret hva.
- **Alternativer:** En delt skytjeneste med innlogging, eller en server på lokalnettet. Begge krever autentisering og personvernarbeid som ligger utenfor v1.0.
- **Ikke dokumentert:** den personlige begrunnelsen for «lokal». **Må bekreftes av prosjekteier.**

## 5. Separate Domain-, Infrastructure- og Web-prosjekter

- **Valg:** Fire prosjekter: `Domain`, `Infrastructure`, `Web`, `Tests`. Avhengighetene går alltid mot Domain.
- **Dokumentert** (`ARCHITECTURE.md`): alternativ «Ett prosjekt / flere lag», begrunnelse «Nok separasjon til å teste regler uten UI/DB, uten overarkitektur». `CLAUDE.md` forbyr nye prosjekter, lag eller abstraksjoner uten forklarbar hensikt og godkjenning.
- **Problem:** Forretningsreglene skal kunne testes uten database og nettleser, og teknologivalg skal ikke spre seg inn i reglene.
- **Fordeler:** Raske enhetstester. `DependencyDirectionTests` håndhever retningen. Lett å se hva som er «regel», «lagring» og «visning».
- **Ulemper:** Flere filer og noe repeterende kode (grensesnitt, `…FormModel`, `ToDetails`). For en nybegynner er det flere steg å følge for én handling. Fire prosjekter er mer enn et så lite program strengt trenger.
- **Alternativer:** Ett Blazor-prosjekt med all kode (raskest å skrive, men reglene blandes med UI og EF), eller flere lag enn tre (for eksempel et eget «Application»-lag; avvist som overarkitektur).

## 6. Repositories og interfaces

- **Valg:** Hver entitet har et repository-grensesnitt i Domain (`IHousingUnitRepository` m.fl.) og en EF-implementasjon i Infrastructure. Tjenestene kjenner bare grensesnittet.
- **Dokumentert** (`ARCHITECTURE.md`): alternativ «Repository for alt», begrunnelse «`DbContext` er allerede en unit of work; interfaces lages når fakes gir testverdi». `CLAUDE.md`: «Repository Pattern kun når det gir konkret nytte».
- **Problem:** Tjeneste- og UI-tester skal ikke trenge en database, og Domain skal ikke kjenne EF Core.
- **Fordeler:** Fake repositories (`FakeTaskItemRepository` osv.) gjør tester raske, deterministiske og i stand til å simulere feil (`IsFailing`).
- **Ulemper:** Mer kode. Kontrakten må vedlikeholdes i to implementasjoner (EF og fake). Fakeen etterligner ikke SQL, så databaseavhengig atferd må dekkes med egne integrasjonstester mot ekte SQLite.
- **Alternativer:** Bruke `DbContext` direkte i tjenestene (enklere, men kobler Domain til EF), eller EF Core InMemory i testene (rask, men håndhever ikke fremmednøkler). Det ble valgt bort til fordel for ekte SQLite i integrasjonstester.
- **Merk:** Det finnes ikke et generisk repository (`IRepository<T>`), bare en liten og konkret kontrakt per entitet.

## 7. `IDbContextFactory` med kortlevd kontekst

- **Valg:** Hver repository-operasjon lager sin egen `DbContext` via `IDbContextFactory<SameieHubDbContext>` og kasserer den etterpå.
- **Dokumentert** (`ARCHITECTURE.md`, «Beslutning: DbContext og levetid»): `AddDbContext` (scoped) ble forkastet fordi «i Blazor Server varer en scope hele brukerøkten (kretsen). Én langlevd kontekst gir utdaterte data og samtidighetsproblemer».
- **Problem:** I Blazor Server lever en krets lenge, og en `DbContext` er ikke ment å leve lenge eller deles av samtidige operasjoner.
- **Fordeler:** Ingen utdaterte objekter i en langlevd kontekst og ingen samtidighetsproblemer.
- **Ulemper:** Objekter er frakoblet fra konteksten som leste dem, så lesing bruker `AsNoTracking()` og lagring bruker `Update(...)` (alle kolonner skrives). Mindre effektivt enn endringssporing, men uten betydning for disse datamengdene.
- **Alternativer:** Scoped `DbContext` (avvist), eller en egen kontekst per side/komponent.

## 8. Fremmednøkler og `RESTRICT`

- **Valg:** Ekte fremmednøkler (`BoardCase.HousingUnitId`, `TaskItem.HousingUnitId`, `TaskItem.BoardCaseId`) med `DeleteBehavior.Restrict`, ingen navigasjonsegenskaper, og `Foreign Keys=True`.
- **Dokumentert** (`ARCHITECTURE.md`, «Permanent sletting av boenheter (M1F)» og «Fremmednøkkel til boenhet»): «aldri cascade».
- **Problem:** Permanent sletting av en boenhet eller styresak skal aldri stille slette eller «foreldreløse» relaterte data.
- **Fordeler:** Databasen er det siste sikkerhetsnettet, selv om en tjeneste skulle omgås. Brukeren får en forståelig melding («knyttet til oppgaver. Fjern koblingene først») og ingenting slettes.
- **Ulemper:** Brukeren må selv fjerne koblinger før sletting. En ekte relasjon uten navigasjonsegenskaper betyr at «vis alle oppgaver for en boenhet» gjøres ved å hente og filtrere i tjenestelaget.
- **Alternativer:** `Cascade` (sletter oppgaver sammen med boenheten: farlig for data), `SetNull` (koblinger forsvinner stille), «myk sletting» med en `IsDeleted`-kolonne (mer logikk overalt).
- **Merk:** Deaktivering av en boenhet (`IsActive`) finnes som den *anbefalte* måten å skjule en boenhet på, siden den beholder alle data.

## 9. Egne domeneregler i entitetene

- **Valg:** Reglene ligger i entitetene (`private set`, validerende konstruktør, `UpdateDetails` som validerer alt først) og i tjenestene (regler som krysser objekter).
- **Dokumentert:** `CLAUDE.md` («Validering og feilhåndtering ved systemgrenser», korte metoder, enkel kode). `ArgumentException.ParamName` brukes som kobling til norske feltmeldinger i UI.
- **Problem:** Samme regel skal gjelde uansett hvilken side eller test som oppretter dataene, og et objekt skal ikke kunne bli ugyldig.
- **Fordeler:** Ett sted per regel, testbart uten UI, og «alt eller ingenting»-oppdateringer.
- **Ulemper:** Mer kode enn attributter (`[MaxLength]`). `ParamName`-kontrakten mellom Domain og Web må holdes i synk manuelt (testet).
- **Alternativer:** `DataAnnotations` på modellen, et valideringsbibliotek som FluentValidation (ikke tatt inn: ingen nye pakker uten god grunn), eller regler i Razor-komponentene (avvist: spres og dupliseres).

## 10. UTC for tidspunkt og norsk kalenderdato

- **Valg:** Tidspunkt lagres i UTC. Datoer uten klokkeslett er `DateOnly`. «I dag» og visning regnes i norsk tid (`NorwegianTime`). «Forfalt» beregnes, aldri lagres.
- **Dokumentert:** `ARCHITECTURE.md` («UTC for tidsstempler», «Forfalt og i dag»).
- **Problem:** Sommertid og at UTC-datoen kan være en dag bak norsk dato (00:00–02:00) kunne gitt feil forfallsstatus.
- **Fordeler:** Entydig lagring, riktig norsk dato i regler og visning, og testbar tid via `TimeProvider`/`ManualTimeProvider`.
- **Ulemper:** En `ValueConverter` trengs fordi SQLite mister `DateTimeKind`. Hver visning må konvertere (`NorwegianDateFormat`). Programmet antar norsk tidssone (`Europe/Oslo`, med `W. Europe Standard Time` som reserve).
- **Alternativer:** Lagre lokal tid (tvetydig ved sommertid), eller bruke `DateTimeOffset`.

## 11. Automatiserte tester

- **Valg:** xUnit for enhets- og integrasjonstester, bUnit for Razor-komponenter, fake repositories i stedet for et mock-bibliotek, ekte SQLite i en temp-fil (ikke EF InMemory), og en arkitekturtest.
- **Dokumentert:** `CLAUDE.md` («TDD for testbar forretningslogikk … Enhetstester med fake repositories; integrasjonstester mot SQLite») og [TESTING.md](TESTING.md). CI kjører alle testene på `windows-latest`.
- **Problem:** Prosjektet utvikles raskt med mange endringer; regresjoner skal oppdages umiddelbart, og eieren skal kunne stole på leveransene.
- **Fordeler:** Over tusen tester kjører på noen sekunder til under minuttet. Oppgraderingstester viser at migreringer bevarer data. Testene dokumenterer reglene.
- **Ulemper:** Testkoden er omtrent like stor som produksjonskoden, og må vedlikeholdes. Noen UI-forhold (reelle museklikk, faktisk layout) dekkes bare av manuell nettlesertest. Antall tester sier ikke noe om hvor godt de dekker virkeligheten.
- **Alternativer:** Færre tester, kun manuell testing, eller mock-biblioteker som Moq (ikke brukt, for å holde testene enkle å lese).
- **Merk om TDD:** CLAUDE.md krever test først med en reell «rød fase». Der dette ikke var mulig (for eksempel at testen ikke kompilerte), er det sagt i leveransen.

## 12. Utvikling i milepæler

- **Valg:** Arbeid deles i milepæler (M0 fundament, M1 boenheter, M2 styresaker, M3 oppgaver, M4 dashboard, M4D demodata, M4E elbillading med delmilepæler, M5 backup/pakking). Hver milepæl avsluttes med en kontrollrapport, og neste milepæl starter først etter eierens uttrykkelige godkjenning.
- **Dokumentert:** `CLAUDE.md` («Milepælbasert arbeid»). Commit-historikken (65 commits, 16.–20. november 2026) følger milepælene.
- **Problem:** Prosjektet har kort tid og en agent som ellers kan gjøre mye på en gang. Eieren skal kunne forstå, kontrollere og styre hver leveranse.
- **Fordeler:** Små, kontrollerbare steg. Mulighet til å justere underveis (for eksempel utvidelsen til to eiere i M1E, og avklaringen av målerne i M4E).
- **Ulemper:** Mer formalitet og mange kontrollrapporter. Planen er ikke fast, og noen krav ble oppdaget først underveis.
- **Alternativer:** Én stor leveranse eller kontinuerlig utvikling uten faste godkjenningspunkter.

## 13. Demoen er isolert fra ordinære data

- **Valg:** Demodata (Fiktivdalen Sameie) lages bare med en eksplisitt kommando og bare i en egen databasefil.
- **Dokumentert:** [DEMO.md](DEMO.md) og kravene i milepælsbeskrivelsene M4D («Demodata skal aldri legges automatisk inn i den ordinære brukerens database»).
- **Problem:** Prosjektet inneholder personopplysninger i den ordinære databasen. Eksempeldata skal aldri blandes med ekte data, og en feil i demo-oppsettet skal ikke kunne skade dem.
- **Fordeler:** Ordinære data er trygge. Demoen kan nullstilles fritt. Flere sperrer (stien må være demostien, bare tom database eller komplett demo, delvis demo gir feil, `reset` nekter hvis demoen kjører).
- **Ulemper:** Flere regler og kode enn en enkel «seed»-knapp. En demo er et øyeblikksbilde: datoene regnes fra opprettelsesdagen, så den må nullstilles for å bli «frisk». En eldre demo (uten nyere innhold) må nullstilles manuelt.
- **Alternativer:** Seeding ved første oppstart (avvist: ville blandet data), eller et eget demoprosjekt (nytt prosjekt krever godkjenning).

## 14. Elbillading: modellvalg (M4E-A)

Domenelaget (M4E-A) og lagringen (M4E-B) er laget. Beslutningene under gjelder modellen; lagringsvalgene står i [ARCHITECTURE.md](ARCHITECTURE.md), avsnittet «Elbillading: lagring (M4E-B)» (eksplisitt transaksjon per operasjon i stedet for en generell Unit of Work, `decimal` som eksakt tekst, `CHECK`-regler og unike indekser).

| Beslutning | Begrunnelse (dokumentert i `ARCHITECTURE.md`, «Beslutninger (M4E-A)») | Ulempe / alternativ |
|---|---|---|
| Ett `Meter`-objekt for både hovedmåler og undermåler (`MeterKind`) | Felles regler og beregninger i `MeterHistory`; ingen duplisering | Alternativ: egne klasser. Felles klasse gir noen felt som bare gjelder den ene typen (`CabinetNumber`, `EvChargerId`). |
| Avlesningstidspunkt som UTC-`DateTime` | Kravet er dato og klokkeslett | Alternativ: bare dato. Brukeren skriver norsk tid som må konverteres i UI. |
| Sikringsskap som `CabinetNumber` 1 eller 2 | Enkelt, og dekker behovet | Alternativ: eget skapregister (avvist av eieren som unødvendig stort). |
| Avlesningssett lagres ikke som egen post | Enklere skjema og database | Alternativ: egen `ReadingSet`-tabell. |
| 24 timers toleranse for «samme tidspunkt» i avstemming | Håndavlesninger skjer sjelden på minuttet | Må bekreftes av prosjekteier (spørsmål stilt etter M4E-A). |
| Ingen estimering av manglende forbruk, ingen proratering | Anslag ville sett ut som målinger | Eventuelt senere som eksplisitt valg. |
| Manglende avlesning er «ikke avlest», aldri 0 kWh | Krav fra eieren («ikke behandles som null») | Null må skrives eksplisitt. |

## 15. Funksjoner som er utsatt

| Funksjon | Status | Hvorfor (dokumentert) |
|---|---|---|
| Backup og gjenoppretting, pakking uten .NET SDK | M5, ikke laget | Obligatorisk før v1.0 (`REQUIREMENTS.md`), men planlagt som egen milepæl. |
| Økonomi og felleskostnader, innlogging/flere brukere, dokumentarkiv, ekstern tilgang, flere sameier, eksterne integrasjoner | Utenfor v1.0 | `REQUIREMENTS.md`, «Utenfor v1.0». |
| Endringshistorikk for styresaker | Ikke laget | `UpdatedAt` viser når, ikke hva (`ARCHITECTURE.md`). |
| Elbillading: UI og demodata | UI er laget i M4E-C (beslutninger i ARCHITECTURE.md, «Elbillading: brukergrensesnitt»); demodata (M4E-D) er ikke laget | Delt i kontrollerbare delmilepæler etter eierens ønske. |
| Integrasjon mot ladeleverandør, fjernstyring, fakturering, målerbytte | Utenfor nåværende plan | Eieren har uttrykkelig utelatt dem; modellen har ingen felt for eksterne nøkler. |

Utsettelsene er gjort for å holde hver milepæl liten nok til å kontrollere og teste, og for å unngå å bygge ting som ikke er bestilt.

## 16. AI-agenten implementerer, eieren godkjenner

- **Valg:** Claude Code (AI-agent) skriver all kode, alle tester og all dokumentasjon i en godkjent milepæl. Eieren beskriver krav, godkjenner milepæler og lærer gjennom forklaringer.
- **Dokumentert:** `CLAUDE.md`, avsnittet «Læring (obligatorisk)»: «Claude implementerer all kode i godkjent milepæl; ingen studentoppgaver som stopper utviklingen. Eieren lærer gjennom forklaringer, kodegjennomgang og egne øvelser utenfor utviklingsflyten».
- **Problem:** Prosjektet skal bli ferdig og brukbart, samtidig som eieren skal forstå koden.
- **Fordeler:** Rask fremdrift, jevn kodekvalitet, mange tester, og forklaringer etter hver delleveranse.
- **Ulemper:** Eieren har ikke skrevet koden selv, og må derfor aktivt lære den (dette dokumentsettet er laget for det). AI-generert kode må kontrolleres: ved at eieren godkjenner milepæler, av testene og CI, og av gjennomgang. Dokumentasjonen skal ikke påstå at eieren skrev koden.
- **Alternativer:** Eieren skriver koden selv med agenten som veileder (tidlig i M1A prøvde arbeidsmåten med «studentoppgaver», men ble endret til dagens, se commit-historikken og `CLAUDE.md`).
- **Må bekreftes av prosjekteier:** hvordan dette skal presenteres overfor lærerne i forhold til kursets regler for bruk av AI.
