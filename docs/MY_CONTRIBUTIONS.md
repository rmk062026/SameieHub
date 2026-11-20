# Mine bidrag til SameieHub

## Om dokumentet

Dette dokumentet beskriver min rolle i planleggingen, utviklingen og kvalitetssikringen av SameieHub, et administrasjonssystem for små norske sameier.

SameieHub er utviklet som en del av C#/.NET-opplæringen min hos Kodehode. Samtidig ønsker jeg å lage et system som er praktisk anvendelig, kan videreutvikles og fungerer som et porteføljeprosjekt.

Prosjektet er utviklet ved hjelp av AI-assistert programmering. Jeg har brukt ChatGPT som teknisk sparringspartner og Claude Code som implementeringsagent.

**Jeg har ikke personlig skrevet den ferdige applikasjonens C#-kode.** Min rolle har vært å arbeide med produktidé, behov, funksjonelle krav, teknologivalg, arkitekturvurderinger, prioriteringer, beslutninger og evaluering av leveranser.

Dette skillet er viktig. Målet er ikke å fremstille AI-generert kode som manuelt skrevet, men å synliggjøre arbeidet som ligger bak planleggingen og styringen av et programvareprosjekt.

Dokumentet bygger på prosjektets dokumentasjon, Git-historikk og beslutninger fra utviklingssamtalene. Personlige læringsresultater må jeg selv kunne forklare og bekrefte.

---

## 1. Bakgrunnen for SameieHub

Jeg ønsket å utvikle et realistisk program fremfor å begrense meg til en liten øvingsoppgave.

Utgangspunktet var et lite sameie med fire tomannsboliger og totalt åtte selvstendige boenheter. Et slikt sameie har behov for oversikt over boliger, eiere, styresaker, vedtak, vedlikehold og fellesoppgaver.

Jeg så muligheten til å samle disse funksjonene i ett administrasjonssystem.

Dette ga meg et prosjekt som kunne brukes til å lære flere viktige deler av programvareutvikling:

- Objektorientert programmering i C#.
- System- og programvarearkitektur.
- Relasjonsdatabaser.
- Brukergrensesnitt.
- Validering og feilhåndtering.
- Testing og kvalitetssikring.
- Versjonskontroll med Git.
- AI-assistert utvikling.

Jeg ønsket samtidig at systemet skulle kunne utvides med mer funksjonalitet senere, uten at vi måtte bygge alt på nytt.

Prosjektet skulle derfor ikke bare demonstrere enkeltstående C#-funksjoner, men hvordan flere deler av et system arbeider sammen.

## 2. Min rolle i prosjektet

Jeg har hatt rollen som prosjekteier og deltatt aktivt i utformingen av løsningen.

Arbeidet mitt har bestått av å definere hva programmet skal gjøre, beskrive hvordan funksjonene bør fungere, diskutere tekniske løsninger og ta stilling til hvilke forslag som skal gjennomføres.

Jeg har også arbeidet med å dele utviklingen inn i mindre milepæler, slik at hver del kan testes og vurderes før neste del bygges.

Jeg har brukt tre ulike former for arbeid:

### Produktutvikling

Jeg har beskrevet behovene systemet skal dekke, foreslått funksjoner, vurdert brukeropplevelsen og endret kravene når nye behov har blitt tydelige.

### Systemarkitektur og tekniske vurderinger

Jeg har diskutert arkitektur og teknologivalg med ChatGPT, vurdert forslag og godkjent den løsningen som skulle implementeres.

Jeg har ikke utviklet alle de tekniske arkitekturmønstrene selv, men har vært delaktig i beslutningene om hvordan de skal brukes i SameieHub.

### AI-assistert implementering

Claude Code har implementert applikasjonen på grunnlag av spesifiserte oppgaver.

Jeg har fulgt utviklingen gjennom kontrollrapporter, automatiske tester, GitHub Actions, kodegjennomgang med ChatGPT og praktisk utprøving av programmet.

Denne arbeidsformen innebærer at jeg ikke nødvendigvis skriver hver kodelinje selv, men at jeg må forstå hva løsningen skal oppnå, hvordan den er bygget opp, og hvorfor tekniske valg er gjort.

---

## 3. Mine bidrag til systemarkitekturen

Arkitekturen har vært en viktig del av prosjektet.

Jeg ønsket en løsning som var forståelig, oversiktlig og mulig å videreutvikle.

Gjennom diskusjoner og vurderinger valgte vi å bygge SameieHub med C#, .NET 10, Blazor Interactive Server, Entity Framework Core og SQLite.

Vi valgte også å organisere løsningen i separate prosjekter.

### Domain

`SameieHub.Domain` inneholder domenemodeller og forretningsregler.

Dette omfatter blant annet regler for gyldige boenheter, styresaker, oppgaver og måleravlesninger.

Jeg har prioritert at slike regler skal være sentralisert og ikke bare eksistere i brukergrensesnittet.

Det gjør at samme regel gjelder uansett hvor i programmet en handling utføres.

### Infrastructure

`SameieHub.Infrastructure` håndterer datalagringen og kommunikasjonen med SQLite gjennom EF Core.

Dette gir et tydelig skille mellom hva programmet skal gjøre, og hvordan informasjonen lagres.

### Web

`SameieHub.Web` inneholder brukergrensesnittet i Blazor.

Dette prosjektet presenterer informasjonen, håndterer brukerhandlinger og bruker tjenestene i domenelaget.

### Tests

`SameieHub.Tests` inneholder automatiserte tester.

Testene dekker blant annet domeneregler, databaseintegrasjon, brukergrensesnitt og sentrale arbeidsflyter.

### Hvorfor jeg støtter denne oppdelingen

Den viktigste begrunnelsen er å holde ulike ansvarsområder adskilt.

Hvis databasekode, validering og brukergrensesnitt blandes i de samme komponentene, blir systemet vanskeligere å forstå, teste og vedlikeholde.

Med dagens oppdeling kan vi teste domeneregler uten å starte applikasjonen eller databasen.

Det blir også enklere å videreutvikle systemet uten at en endring nødvendigvis påvirker alle andre deler.

**Min arkitekturbidragsrolle har vært å vurdere, velge og videreutvikle denne strukturen gjennom samarbeid med AI-verktøyene.** Den konkrete implementasjonen av klassene, grensesnittene og avhengighetene er utført av Claude Code.

Se [ARCHITECTURE_EXPLAINED.md](ARCHITECTURE_EXPLAINED.md) for en teknisk gjennomgang.

---

## 4. Teknologivalg og vurderinger

### C# og .NET 10

Jeg ønsket å bruke C#/.NET-kunnskapen fra Kodehode i et større, sammenhengende prosjekt.

Ved å arbeide med flere moduler får jeg erfaring med hvordan klasser, objekter, grensesnitt, tjenester og databaser brukes i praksis.

### Blazor fremfor et tradisjonelt skrivebordsprogram

Vi diskuterte ulike måter å bygge brukergrensesnittet på, inkludert et tradisjonelt Windows-program.

For den første versjonen valgte vi Blazor Interactive Server.

Det gjorde det mulig å lage et responsivt grensesnitt og samtidig holde løsningen som én lokal applikasjon uten separat API.

En ulempe er at applikasjonen foreløpig starter en lokal webserver og åpnes i nettleseren.

Dette ble tydelig da jeg selv skulle starte demoversjonen. Jeg ønsket derfor at en senere Windows-utgave skal kunne åpnes med vanlig dobbeltklikk.

Dette er et eksempel på hvordan praktisk testing påvirker de videre tekniske kravene.

### SQLite og Entity Framework Core

Vi valgte SQLite fordi SameieHub i første omgang skal brukes lokalt.

En SQLite-database krever ikke en separat databaseserver, samtidig som den støtter relasjoner, fremmednøkler og SQL.

EF Core brukes til å koble C#-objektene til databasen og håndtere migreringer.

Jeg har også vært opptatt av datalagring og SQL som en sentral del av backend-utvikling, ikke bare noe brukergrensesnittet skal forholde seg til.

### Lokal applikasjon

Første versjon skal være enkel å starte og bruke uten egen serverdrift, skyhosting eller innlogging.

Dette begrenser kompleksiteten i prosjektet.

Samtidig betyr det at tilgang fra flere brukere, deling mellom enheter og ekstern tilgang må vurderes i en senere versjon.

### Testing og vedlikeholdbarhet

Vi valgte å bruke xUnit, bUnit, integrasjonstester med SQLite og GitHub Actions.

Jeg har prioritert at hver milepæl skal levere testbar funksjonalitet, ikke bare kode som kompilerer.

Mer detaljerte tekniske begrunnelser finnes i [DESIGN_DECISIONS.md](DESIGN_DECISIONS.md).

---

## 5. Mine konkrete funksjonelle bidrag

En viktig del av arbeidet har vært å beskrive hvordan programmet faktisk skal fungere for et lite sameie.

### Boenheter

Jeg ønsket et boligregister som gjenspeiler hvordan boligene er organisert.

Det innebar blant annet:

- Separate boenheter i tomannsboliger.
- Adresser og seksjonsnummer.
- Areal og boligtype.
- Mulighet for to eiere.
- Kontaktopplysninger.
- Merknader.
- Aktive og inaktive boenheter.

Et viktig poeng var at en boenhet kan bli inaktiv uten at all historikken skal forsvinne.

### Styresaker

Jeg ønsket å registrere saker som styret behandler, og å kunne knytte relevante saker til en bestemt boenhet.

Sakene skulle ha status, prioritet, beskrivelse og mulighet for vedtak.

Under utviklingen ble det også lagt til vedtaksdato, slik at informasjon om når et vedtak ble gjort, kunne registreres.

### Oppgaver og vedlikehold

Jeg ønsket at styresaker skulle kunne føre til konkrete oppgaver.

Oppgaver skulle blant annet ha:

- Tittel og beskrivelse.
- Ansvarlig person.
- Prioritet.
- Frist.
- Status.
- Mulighet for fullføring og gjenåpning.
- Kobling til boenhet og/eller styresak.

Dette gjør det mulig å gå fra en sak som er diskutert av styret, til et konkret arbeid som skal utføres.

### Dashboard

Jeg ønsket en startside som samler informasjon fra de ulike modulene.

Målet var å gjøre det raskere å finne åpne saker, oppgaver som haster og kommende frister.

Dashboardet skal være en oversikt, mens detaljerte handlinger skjer i de respektive modulene.

---

## 6. Elbillading – et eksempel på mine bidrag til datamodell og systemdesign

Elbillading er et godt eksempel på hvordan mine egne innspill har påvirket prosjektet.

I det faktiske boligoppsettet er det åtte carporter med hver sin elbillader.

I den første demoversjonen lå informasjon om laderne i merknadsfeltet til hver boenhet.

Da jeg startet og prøvde programmet selv, opplevde jeg at dette ikke ga god nok oversikt.

Jeg ønsket derfor en separat modul for elbillading.

### Første behov

Jeg beskrev at hver boenhet burde kunne vise om elbilladeren var i bruk, ikke i bruk eller midlertidig deaktivert.

Jeg ønsket også å kunne registrere strømforbruk manuelt og se historiske verdier.

### Nye krav fra det fysiske anlegget

Etter videre diskusjon presiserte jeg hvordan ladeanlegget faktisk er bygget opp:

- Én felles hovedmåler.
- To sikringsskap.
- Fire separate undermålere i hvert skap.
- Totalt åtte fysiske undermålere.
- Én undermåler knyttet til hver boenhets lader.

Undermålerne er separate fysiske målere i sikringsskapene, ikke innebygde energimålere i ladeboksene.

Dette var viktig fordi en løsning med åtte uavhengige ladebokser ikke fullt ut representerer det faktiske målesystemet.

### Konsekvensen for programmet

Vi utvidet derfor den opprinnelige planen.

Systemet skal kunne registrere målerstander på hovedmåleren og alle undermålerne.

Deretter skal det beregne forbruket mellom avlesninger og sammenligne summen av undermålerforbruket med hovedmålerforbruket.

Dette krever at vi skiller mellom akkumulerte målerstander og faktisk forbruk i en periode.

Vi må også håndtere manglende avlesninger og ulike tidspunkt.

Eksemplet viser hvordan domenekunnskap og praktiske krav kan påvirke en teknisk modell.

**Jeg har bidratt med behovsbeskrivelsen, kunnskap om det fysiske anlegget og videreutvikling av kravene.** Selve domenemodellen og beregningskoden er implementert av Claude Code.

Ved dokumentasjonens opprinnelige ferdigstillelse var bare domenelaget for elbillading (M4E-A) implementert. Database, brukergrensesnitt og demodata gjenstår i denne statusbeskrivelsen.

---

## 7. Hvordan jeg organiserte utviklingen

Jeg har valgt en milepælsbasert arbeidsform.

Hensikten har vært å utvikle én avgrenset del av systemet om gangen og kontrollere den før vi fortsetter.

| Milepæl | Innhold |
|---|---|
| M0 | Prosjektgrunnlag, arkitektur og utviklingsmiljø |
| M1 | Boenheter og eiere |
| M2 | Styresaker og vedtak |
| M3 | Oppgaver og vedlikehold |
| M4 | Dashboard |
| M4D | Realistisk og isolert demodatabase |
| M4E | Elbillading, delt i mindre tekniske faser |
| M5 | Planlagt backup, gjenoppretting og Windows-distribusjon |

Arbeidsprosessen har fulgt et fast mønster:

1. Diskutere behov og tekniske alternativer.
2. Bestemme hva milepælen skal inneholde.
3. Utforme konkrete krav og avgrensninger.
4. La Claude Code implementere den avtalte delen.
5. Motta kontrollrapport med testresultater og endringer.
6. Gjennomgå resultatene, eventuelt med uavhengig kodekontroll i ChatGPT.
7. Be om rettelser der det er nødvendig.
8. Godkjenne videre utvikling.

Dette har gjort utviklingen mer oversiktlig og redusert risikoen for at mange forskjellige endringer blandes sammen.

---

## 8. Hvordan jeg bruker ChatGPT og Claude Code

AI-verktøyene har hatt forskjellige oppgaver i prosjektet.

### ChatGPT – planlegging og teknisk sparring

Jeg bruker ChatGPT til å diskutere idéer, funksjonalitet og tekniske løsningsalternativer.

Dette inkluderer blant annet:

- Diskusjoner om teknologistakk og arkitektur.
- Utforming av funksjonskrav.
- Vurdering av hva som bør prioriteres.
- Planlegging av milepæler.
- Utarbeidelse av detaljerte oppgaver til Claude Code.
- Gjennomgang av rapporter.
- Kontroll av utvalgte kildefiler og GitHub Actions.
- Vurdering av feil, forbedringer og videreutvikling.

ChatGPT har også bidratt med tekniske anbefalinger som jeg har vurdert og tatt stilling til.

### Claude Code – implementering

Claude Code har hatt ansvaret for å skrive og endre applikasjonskoden.

Agenten har blant annet:

- Opprettet C#-klasser og grensesnitt.
- Implementert tjenester.
- Laget repositories og EF Core-migreringer.
- Utviklet Blazor-komponenter.
- Skrevet automatiserte tester.
- Gjennomført nettlesertester.
- Oppdatert dokumentasjon.
- Opprettet commits og pushet endringene til GitHub.

Agenten har arbeidet innenfor avgrensede milepæler og stoppet for godkjenning før neste del.

### Min rolle mellom verktøyene

Det er jeg som vurderer hvilke problemer vi skal løse, hvilke funksjoner som er ønskelige, og om vi skal fortsette med en foreslått løsning.

Jeg bruker dermed AI-verktøyene i en arbeidsprosess hvor planlegging, vurdering og implementering er skilt fra hverandre.

Denne arbeidsformen stiller også krav til min egen tekniske forståelse. Jeg må kunne vurdere om kravene er riktige, forstå sentrale arkitekturvalg og kunne forklare hvordan systemet fungerer.

---

## 9. Kvalitetssikring og testing

Jeg har ønsket at prosjektet skal utvikles med kontinuerlig kvalitetssikring.

### Automatiske tester

Claude Code har skrevet enhetstester, integrasjonstester og tester for brukergrensesnittet.

Disse brukes til å kontrollere domeneregler, lagring, validering, relasjoner og brukerhandlinger.

GitHub Actions kjører testene automatisk etter endringer.

Ved dokumentasjonens opprinnelige ferdigstillelse bestod 1040 automatiske tester.

Dette er ikke det samme som at jeg personlig har skrevet eller gjennomført alle testene.

### Kodegjennomgang

Jeg har brukt ChatGPT til å gjennomgå utvalgte deler av kildekoden direkte fra GitHub.

Et konkret eksempel var gjennomgangen av demodatabasen etter M4D.

ChatGPT identifiserte svakheter knyttet til kontroll av databasebane og verifisering av komplett demo-seeding.

Jeg valgte å prioritere rettelsene før videre utvikling.

Claude Code implementerte deretter forbedringene og tilhørende tester.

Dette viser hvordan jeg har brukt et ekstra kontrollledd fremfor å basere meg utelukkende på implementeringsagentens egen rapport.

### Praktisk testing

Jeg har startet demoversjonen lokalt på min egen PC.

Under denne gjennomgangen opplevde jeg blant annet at enkelte funksjoner manglet tilstrekkelig oversikt.

Det førte til ønsket om en separat elbillademodul.

Jeg oppdaget også at oppstarten gjennom PowerShell var mindre brukervennlig enn ønsket, og at programmet burde kunne startes enklere i en ferdig Windows-versjon.

Den praktiske gjennomgangen har derfor ikke bare handlet om hvorvidt programmet kjører, men også hvordan det oppleves for brukeren.

---

## 10. Hvordan prosjektet har utviklet seg gjennom egne vurderinger

Prosjektets funksjonalitet har ikke vært fullstendig bestemt fra starten.

Flere krav har kommet frem etter diskusjoner og praktisk bruk.

### Eksempel 1: To eiere

En første versjon av et boligregister kan være enkel, men et faktisk sameie kan ha flere eiere per boenhet.

Derfor ønsket jeg at boenhetsmodellen skulle støtte to eiere med egne kontaktopplysninger.

### Eksempel 2: Koblinger mellom modulene

Styresaker, oppgaver og boenheter skal ikke være tre helt separate registre.

Jeg ønsket at det skulle være mulig å se hvilke oppgaver som hører til en styresak eller en boenhet.

### Eksempel 3: Fiktiv demodatabase

Jeg ønsket å fylle programmet med realistiske eksempeldata for å få bedre oversikt over hvordan funksjonene brukes.

Derfor ble det opprettet et fiktivt sameie med åtte boenheter, tolv styresaker og tjue oppgaver.

Demoen er isolert fra den ordinære databasen.

### Eksempel 4: Sikkerhet ved demo-seeding

Under kontroll ble det oppdaget at demo-seeding burde ha strengere begrensninger.

Jeg valgte å få dette rettet før vi fortsatte.

### Eksempel 5: Elbillading

Etter å ha testet demoen mente jeg at elbillading fortjente en egen modul.

Det praktiske måleroppsettet førte til at denne funksjonen ble mer omfattende enn først planlagt.

Disse eksemplene viser at prosjektet utvikles gjennom behov, vurderinger, tilbakemeldinger og nye beslutninger.

---

## 11. Hva jeg arbeider med å lære

Jeg ønsker at dette prosjektet skal gi mer enn erfaring med å få en AI-agent til å produsere kode.

Jeg arbeider med å forstå hvordan programvaren er bygget opp, og hvorfor de forskjellige delene finnes.

Viktige tema jeg ønsker å kunne forklare er:

- Forskjellen mellom en klasse og et interface.
- Hvordan objekter og properties brukes i C#.
- Hvorfor `private set` kan beskytte domenemodeller.
- Hvordan `async` og `await` fungerer.
- Hvordan Dependency Injection kobler sammen tjenester.
- Hvorfor vi bruker repositories.
- Hvordan EF Core mapper objekter til SQLite.
- Hvordan fremmednøkler beskytter relasjoner.
- Hvorfor databasekontekster bør ha riktig levetid.
- Hvordan validering og feilhåndtering fungerer.
- Hvordan enhetstester og integrasjonstester skiller seg.
- Hvordan UTC og norsk kalenderdato håndteres.
- Hvordan en endring beveger seg gjennom hele systemet.

Jeg ser også at arkitekturforståelse og evnen til å lese kode blir viktig når AI brukes til implementering.

Målet mitt er å kunne forklare en funksjon fra brukergrensesnittet, gjennom domenelogikken og videre til databasen.

Jeg ønsker også å kunne begrunne hvorfor vi valgte én løsning fremfor et alternativ.

**Dette er læringsmål og områder jeg arbeider med, ikke en påstand om at jeg allerede behersker alt fullt ut.**

---

## 12. Min forståelse av AI-assistert utvikling

Jeg ser på AI-agenter som en viktig del av moderne programvareutvikling.

AI kan bidra med kode, tester, dokumentasjon og feilsøking, men resultatet er fortsatt avhengig av tydelige krav, fornuftige tekniske beslutninger og kvalitetssikring.

Jeg mener derfor det er relevant å lære både programmering og hvordan AI-verktøy kan brukes i en profesjonell utviklingsprosess.

For meg har SameieHub vært en mulighet til å arbeide med begge deler.

Jeg har valgt å la Claude Code håndtere implementasjonen, samtidig som jeg bruker prosjektet til å utvikle min forståelse av C#, arkitektur, datamodellering og systemutvikling.

Jeg mener dette er en relevant arbeidsmetode, men den erstatter ikke behovet for å forstå kildekoden.

Hvis en AI-agent gjør en feil, er det viktig å kunne oppdage den, forstå konsekvensene og vurdere hvordan den skal rettes.

Jeg ønsker derfor å kunne stå inne for de tekniske løsningene som brukes i prosjektet, selv om jeg ikke har skrevet hver enkelt linje selv.

---

## 13. Hva jeg ville gjort annerledes

Prosjektet har også vist noen svakheter ved arbeidsmåten.

Det har vært mulig å utvikle funksjonalitet raskt, men det betyr at jeg må sette av egen tid til å sette meg grundigere inn i koden.

Jeg ønsker derfor å bruke den tekniske dokumentasjonen til å gå gjennom prosjektets viktigste klasser, metoder og dataflyter.

Jeg vil også prioritere mer praktisk testing underveis, ikke bare etter at flere milepæler er fullført.

En annen erfaring er at brukervennlighet bør vurderes tidlig. Oppstarten av demoversjonen viste at en teknisk fungerende løsning ikke nødvendigvis er den enkleste løsningen for en vanlig bruker.

Dette er forhold jeg ønsker å ta med meg videre.

---

## 14. Videreutvikling

SameieHub er planlagt videreutviklet med:

### Elbillading

Fullføre database, brukergrensesnitt, måleravlesninger og avstemming mellom hovedmåler og undermålere.

### Sikkerhetskopiering og gjenoppretting

Sørge for at registrerte data kan sikkerhetskopieres og gjenopprettes trygt.

### Windows-distribusjon

Lage en selvstendig versjon som kan startes uten utviklerverktøy.

### Mulige senere funksjoner

- Dokumentarkiv for sameiet.
- Mer omfattende møteadministrasjon.
- Administrasjon av carporter.
- Automatisk innhenting av målerstander.
- Strømpriser og eventuell kostnadsfordeling.
- Mer detaljert historikk.
- Mulighet for flere brukere og ekstern tilgang.

Disse funksjonene er mulige videreutviklinger og er ikke nødvendigvis en del av den første ferdige versjonen.

---

## 15. Oppsummering av mine bidrag

SameieHub er et AI-assistert utviklingsprosjekt der min rolle har vært å bidra til å planlegge, forme og styre utviklingen.

Mine viktigste bidrag er:

1. Utforming og videreutvikling av produktidéen.
2. Identifisering av behov og funksjonelle krav.
3. Vurdering og godkjenning av systemarkitektur og teknologivalg.
4. Bidrag til datamodellering gjennom kunnskap om det faktiske bruksområdet.
5. Prioritering av funksjoner og avgrensning av prosjektet.
6. Planlegging og oppfølging av milepæler.
7. Bruk av AI-agenter til implementering og kontroll.
8. Vurdering av testresultater og kvalitet.
9. Praktisk utprøving og tilbakemeldinger på brukeropplevelsen.
10. Arbeid med å forstå og kunne forklare koden og arkitekturen.

Jeg ønsker å være tydelig på at AI-agenten har skrevet implementasjonen. Samtidig mener jeg det er viktig å synliggjøre arbeidet med å definere, vurdere og videreutvikle selve systemet.

SameieHub representerer derfor både et praktisk programvareprosjekt og en læringsprosess innen C#, systemarkitektur og AI-assistert utvikling.

---

## 16. Dokumentasjon og videre lesing

For en mer teknisk gjennomgang av prosjektet:

- [PROJECT_STRUCTURE.md](PROJECT_STRUCTURE.md) – Prosjektstruktur, mapper og filer.
- [ARCHITECTURE_EXPLAINED.md](ARCHITECTURE_EXPLAINED.md) – Hvordan arkitekturen fungerer.
- [CODE_WALKTHROUGH.md](CODE_WALKTHROUGH.md) – Gjennomgang av sentral C#-kode.
- [DESIGN_DECISIONS.md](DESIGN_DECISIONS.md) – Teknologivalg og arkitekturbeslutninger.
- [REQUIREMENTS.md](REQUIREMENTS.md) – Krav og funksjonelle avgrensninger.
- [TESTING.md](TESTING.md) – Teststrategi.
- [DEMO.md](DEMO.md) – Demonstrasjonsdatabase.
- [PRESENTATION_GUIDE.md](PRESENTATION_GUIDE.md) – Presentasjonsforberedelser og kontrollspørsmål.

Den tekniske dokumentasjonen bør leses sammen med kildekoden for å forstå både arkitekturen og implementasjonen.

**Merk:** Beskrivelsen er et personlig prosjektutkast. Før den brukes i endelig innlevering, bør jeg kontrollere at alle formuleringene samsvarer med mine egne erfaringer og det jeg faktisk kan forklare.