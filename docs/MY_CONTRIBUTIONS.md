# Mine bidrag til SameieHub

> **Viktig om dette dokumentet.** Det er skrevet i førsteperson som et *utkast* til prosjektfortelling, men det er laget av AI-agenten ut fra det som faktisk kan kontrolleres: prosjektets filer, commit-historikken og meldingene jeg (prosjekteieren) har sendt i samtalene med agenten. Det er **ikke** en ferdig tekst jeg kan levere uten gjennomgang.
>
> - **[Verifisert]** betyr at påstanden kan kontrolleres mot en kilde som oppgis (fil, commit eller en melding jeg har sendt i samtalen).
> - **[Forslag]** betyr tekst jeg selv må lese, rette og bekrefte før jeg bruker den. Her står **«Må bekreftes av prosjekteier»**.
> - Jeg har **ikke** skrevet koden. All C#-kode, alle tester og alt dokumentasjonsmateriale i repoet er skrevet av AI-agenten (Claude Code), styrt av kravene og godkjenningene mine. Det står i `CLAUDE.md` og går igjen i hele historikken.
>
> Kilder: [CLAUDE.md](../CLAUDE.md), [REQUIREMENTS.md](REQUIREMENTS.md), [ARCHITECTURE.md](ARCHITECTURE.md), [DESIGN_DECISIONS.md](DESIGN_DECISIONS.md) og `git log` (65 commits 16.–20. november 2026, alle under identiteten `rmk062026`; 63 av dem har en `Co-Authored-By: Claude`-linje).

## Hva jeg ville lage, og hvorfor

**[Verifisert]** `CLAUDE.md` beskriver SameieHub som et «lokalt administrasjonsprogram for et lite sameie (selveide boliger i tomannsboliger)». Det er en Kodehode-innlevering, «men også et brukbart produkt og porteføljeprosjekt», og eieren «lærer C#/.NET». `REQUIREMENTS.md` beskriver sameiet som boliger i tomannsboliger med egne husnummer, private uteområder og fellesarealer.

**[Forslag, må bekreftes av prosjekteier]**
> Jeg ville lage et enkelt, lokalt verktøy for et lite sameie som i dag mangler en samlet oversikt over boenheter, styresaker, vedtak, vedlikehold og, senere, elbillading. Jeg ønsket å løse et reelt praktisk behov og samtidig lære C#/.NET gjennom et prosjekt jeg kunne vise frem.

Hvorfor akkurat dette sameiet og hvilke praktiske problemer som ligger bak (for eksempel hvordan styresaker og oppgaver håndteres i dag, og hvorfor elbilladingen trenger egne målere), er **ikke dokumentert** i repoet. *Må bekreftes av prosjekteier.* Meldingen om elbillading og demodata nevner «naboene», så sameiet er sannsynligvis et ekte sameie, men det står ikke eksplisitt.

## Hvordan jeg definerte kravene

**[Verifisert]** Jeg ga agenten en detaljert milepælsbeskrivelse for hver leveranse (M0, M1A–M1F, M2A–M2E, M3A–M3D, M4, M4D, M4E). Beskrivelsene definerer:
- hva som skal bygges og ikke bygges (for eksempel «ingen nye prosjekter eller UI-biblioteker uten godkjenning», «ikke start M5»),
- teknologistakken (C#, .NET 10, Blazor Interactive Server, SQLite/EF Core, xUnit, GitHub Actions) og regler for kodestil og git (Conventional Commits, ingen force-push, ingen databasefiler i repoet, fiktive testdata),
- krav til personvern (personopplysninger bare i den lokale databasen, aldri i logger, tester, seed-data eller commits),
- hva som skal rapporteres ved milepælens slutt (struktur, filer, build/test, CI, commits, begreper, kontrollspørsmål).

`REQUIREMENTS.md` og `CLAUDE.md` inneholder avgrensningene: ingen økonomi, flere brukere, dokumentarkiv, ekstern tilgang, flere sameier eller eksterne integrasjoner i v1.0.

## Hvordan jeg prioriterte

**[Verifisert]** Rekkefølgen i milepælene er: boenheter først (fordi alt annet peker på dem), så styresaker, så oppgaver, så dashboard, så demodata, så elbillading. Backup/gjenoppretting og pakking (M5) er bevisst plassert sist, men står som **obligatorisk før v1.0** i `REQUIREMENTS.md`.

**[Forslag, må bekreftes av prosjekteier]** Hvorfor jeg valgte akkurat denne rekkefølgen, og hvorfor elbillading kom *etter* dashboardet og demoen.

## Hvordan jeg brukte AI-agenter

**[Verifisert]**
- Jeg brukte Claude Code som utvikler. Agenten leser `CLAUDE.md`, jobber innenfor én godkjent milepæl av gangen, skriver kode og tester, kjører build og tester, committer og pusher, og avslutter med en kontrollrapport og en forklaring på norsk.
- Jeg endret arbeidsmåten tidlig: i M1A var planen at jeg skulle skrive en liten del selv («studentoppgaver»). Jeg bestemte så at agenten skal implementere all kode, og at jeg lærer gjennom forklaringer, kodegjennomgang og egne øvelser utenfor utviklingsflyten (`CLAUDE.md`, «Læring (obligatorisk)»).
- Jeg ba om enkel, lesbar C# som en fersk C#-utvikler kan forstå (korte metoder, `foreach` fremfor kompleks LINQ, kommentarer bare for *hvorfor*), og at undervisning gis i samtalen eller i `docs/`, ikke som kommentarer i koden.
- Agenten stopper ved milepælens slutt og venter på min uttrykkelige godkjenning.

**[Forslag, må bekreftes av prosjekteier]** Hvordan jeg konkret kontrollerer at agentens arbeid stemmer: leser jeg kontrollrapportene og diffene, kjører jeg programmet selv, eller stoler jeg hovedsakelig på testene og CI? Dette må jeg selv beskrive.

## Hvordan utviklingen ble delt i milepæler

**[Verifisert]** (commit-historikken)

| Dato | Milepæl | Innhold |
|---|---|---|
| 16. nov | M0 | Fundament: løsning, prosjekter, CI, dokumentasjon, kodestil |
| 17. nov | M1A–M1E | Boenhet, tjeneste og repository, SQLite/EF Core, Blazor-UI, utvidede felt med to eiere |
| 18. nov | M1F, M2A–M2E | Sikker sletting, styresaker (domene, lagring, UI), vedtaksdato |
| 19. nov | M3A–M3D, M4, M4D | Oppgaver (domene, lagring, UI), dashboard, demodata og sikkerhetsrettelser |
| 20. nov | M4E-A | Domenemodell for elbillading (bare domene) |

Elbillading ble delt i delmilepæler (A: domene, B: database, C: UI, D: demo) etter at jeg skrev at omfanget var for stort for én leveranse. Bare A er ferdig.

## Hvordan jeg vurderte og godkjente leveranser

**[Verifisert]** (mine meldinger i samtalen)
- Jeg godkjente milepæler eksplisitt før neste startet, for eksempel «Jeg godkjenner M2 som ferdig», «Jeg godkjenner M3B», «Jeg godkjenner M3C og starter M3D» og «Jeg godkjenner M3 som fullført og starter M4». Om M4D skrev jeg at den var «funksjonelt godkjent», men ba om rettelser før M5.
- Hver leveranse hadde kontrollspørsmål og en kontrollrapport med endrede filer, testresultat og CI-status.

**[Forslag, må bekreftes av prosjekteier]** Hva jeg selv sjekket i hver leveranse, utover å lese rapportene.

## Hvordan tilbakemeldingene mine påvirket produktet

**[Verifisert]** (mine meldinger og påfølgende endringer i repoet)

| Min tilbakemelding | Effekt |
|---|---|
| Agenten skal implementere all kode (M1A/M1B) | Arbeidsmåten i `CLAUDE.md`; enkel, lesbar kodestil |
| To eiere per boenhet, valgfrie felt, begge eiere søkbare (M1E) | `HousingUnit` fikk eier 1 og eier 2 med navn, telefon og e-post |
| Sikker permanent sletting (M1F) | Bekreftelse, sletting blokkert når andre poster peker på den, ingen cascade |
| Vedtaksdato på styresaker (M2E) | Ny kolonne og migrering som beholder eksisterende data |
| Kobling mellom oppgaver, boenheter og styresaker, og snarveier (M3D) | `TaskItem` med to valgfrie koblinger og seksjoner på sidene til boenhet og styresak |
| Demodata må aldri havne i den vanlige databasen (M4D) | Eksplisitt kommando, egen databasefil og sperrer |
| Kodegjennomgang fant to svakheter i demo-seeding (egen melding etter M4D) | Streng sti-kontroll, skille mellom komplett/delvis demo, trygg `reset` |
| Elbilladingen skal ha hovedmåler og åtte undermålere i to skap, manuell registrering, aldri summere målerstander (M4E) | Hele målermodellen og avstemmingen i M4E-A |

**Må bekreftes av prosjekteier:** hvem som utførte den kodegjennomgangen jeg nevnte etter M4D (meg selv, en lærer eller et verktøy).

## Hvordan applikasjonen er testet

**[Verifisert]**
- **Automatiske tester:** 1040 tester (xUnit og bUnit) er skrevet av agenten og kjører i CI (GitHub Actions) ved hver push. Se [TESTING.md](TESTING.md).
- **Nettlesertest:** Agenten startet appen mot en isolert database og styrte en nettleser med faktiske klikk og tastetrykk på PC og ved 375 px bredde, og rapporterte hva som ble funnet og rettet (for eksempel to mobilfeil i M4D).
- Min vanlige AppData-database er aldri brukt til testing (en regel jeg satte, og som agenten har kontrollert at ikke ble brutt).

**[Verifisert, men uten detaljer]** Jeg skrev at jeg hadde «testet programmet selv» før M4E. Hva jeg testet og hva jeg fant, er ikke dokumentert i repoet. **Må bekreftes av prosjekteier** før det brukes som en påstand om egen testing.

## Hva jeg har lært

**[Forslag, må bekreftes av prosjekteier. Skriv om med egne ord.]**
> Jeg har lært hvordan et .NET-program deles i lag (Domain, Infrastructure, Web), hvorfor man bruker grensesnitt og Dependency Injection, hvordan EF Core og SQLite fungerer sammen med migreringer og fremmednøkler, og hvorfor tid bør lagres i UTC. Jeg har lært å styre en AI-agent med tydelige krav, milepæler og godkjenningspunkter, og å stille kontrollspørsmål til det den leverer. Dokumentene [ARCHITECTURE_EXPLAINED.md](ARCHITECTURE_EXPLAINED.md) og [CODE_WALKTHROUGH.md](CODE_WALKTHROUGH.md) er mine studiemateriell for å kunne forklare koden selv.

Det er et mål at jeg **kan forklare** koden, ikke bare at den finnes. Kontrollspørsmålene i hver milepælsrapport og spørsmålene i [PRESENTATION_GUIDE.md](PRESENTATION_GUIDE.md) er øvelser til det.

## Hva jeg ønsker å videreutvikle

**[Verifisert]** (fra planen og milepælsbeskrivelsene)
- Elbillading ferdig: database og migrering (M4E-B), brukergrensesnitt med samlet registrering av målerstander og avstemming (M4E-C), og utvidet demo (M4E-D).
- Backup og gjenoppretting av databasen og pakking for kjøring uten .NET SDK (M5).
- Mulige senere ideer som er dokumentert som utenfor v1.0: integrasjon mot et eksternt ladesystem, ladehistorikk, endringshistorikk for styresaker.

## Åpne punkter jeg må ta stilling til

1. Hvorfor jeg ville lage SameieHub, og hvilket konkret problem i sameiet det løser.
2. Om C#/.NET og stakken var oppgavens krav eller mitt eget valg.
3. Hva jeg testet selv, og hvem som gjorde kodegjennomgangen etter M4D.
4. Hvordan jeg presenterer bruken av AI-agenter overfor lærerne i forhold til kursets regler.
5. Hva jeg faktisk har lært (jeg må formulere det med egne ord, og kunne forklare det).
6. Toleransen på 24 timer for sammenlignbare målerlesninger og andre avklaringer som ble stilt etter M4E-A.
