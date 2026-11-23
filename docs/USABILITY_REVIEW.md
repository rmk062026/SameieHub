# Brukervennlighet og fremtidig bruk (M4T-P1.2)

Dette er en **gjennomgang og anbefaling**. Ingen funksjonelle endringer er gjort på grunnlag av den. Del A (oppstartstest) er det eneste som er implementert i dette steget og er beskrevet til slutt.

**Utgangspunkt:** SameieHub skal kunne brukes som et reelt administrasjonssystem for et lite norsk sameie (første målgruppe: åtte boenheter i fire tomannsboliger), av personer uten IT-bakgrunn. Prinsippet for anbefalingene er at **et tiltak bare foreslås når det løser et faktisk problem som er vist**. Det er ikke et mål å gjøre programmet større.

## 1. Slik er gjennomgangen gjort

- **Nettleser (merket [N]):** jeg har gått gjennom dashboard, boenheter, styresaker, oppgaver, elbillading og skjemaene i den innebygde nettleseren, mot en **kopi** av den fiktive demodatabasen (din demodatabase og den ordinære databasen er ikke endret). Jeg har både klikket (fullføre en oppgave) og lest sidene som et styremedlem ville. Smal skjerm er testet med 375 px bredde på 15 sider, og en tom database (førstegangsbruk) er sett.
- **Kildekode (merket [K]):** funn som er lest i Razor-sider og domenekode og ikke ellers vist.
- **Begrensning:** nettleservinduet er omtrent 1024 px bredt (typisk liten bærbar). Ingen ekte sameiebrukere har testet programmet. Anbefalingene er derfor **forslag til tester med ekte brukere**, ikke fasit. Demodatabasen er også noen dager gammel, så den viser mange forfalte oppgaver (7). Det er en følge av at datoene regnes fra opprettelsesdagen, ikke et programproblem.

### Prioritetene

| Prioritet | Betydning |
|---|---|
| **P0** | Må være på plass før første reelle bruk (data eller bruk er ellers ikke trygt) |
| **P1** | Bør rettes før første reelle bruk; vanlige brukere vil støte på det |
| **P2** | Forbedring som merkes, men programmet er brukbart uten |
| **P3** | Mindre ting, vurder senere |

«Type» er **E** (justering av eksisterende funksjon) eller **N** (nytt funksjonsønske). Det er bare én N i listen, og den er merket tydelig.

## 2. De fem viktigste funnene

1. **Tabellene skjuler det viktigste på vanlige skjermer.** I oppgavelisten ligger *Ansvarlig* og *Handlinger* utenfor synsfeltet ved 1024 px, og i styresaklisten er *Handlinger* skjult. På mobil må alle listene rulles sideveis. [N] (F-04, F-05)
2. **Styresaklisten viser ikke møtedato**, men to tidsstempler med klokkeslett (*Opprettet*, *Sist oppdatert*) som styret sjelden trenger. [N][K] (F-03)
3. **Å fullføre en oppgave er skjult.** Knappen ligger nederst på detaljsiden, og bekreftelsen vises øverst, utenfor skjermen. [N] (F-07)
4. **Avstemmingen forklarer ikke hva differansen betyr.** «+54,5 %» står uten forklaring, og brukeren får ikke vite hva som er normalt eller hva man skal gjøre. [N] (F-10)
5. **Ingen backup og ingen enkel oppstart ennå.** Programmet lagrer navn og telefonnumre i én lokal fil uten sikkerhetskopi, og krever .NET SDK og kommandolinje for å starte (M5 og M5C). Dette er ikke en feil i dag, men må løses **før** reell bruk. [K] (F-01, F-02)

## 3. Funn per område

### Helhet og første reelle bruk

| ID | Funn | Hvor | Anbefaling | P | Nytte for en sameiebruker | Type | Før første reelle bruk? |
|---|---|---|---|---|---|---|---|
| F-01 | **Ingen sikkerhetskopi.** Alle data ligger i `%LOCALAPPDATA%\SameieHub\sameiehub.db`. Mister man maskinen eller filen, mister man register, vedtak og målerhistorikk. [K] | Hele programmet (M5, krav i `REQUIREMENTS.md`) | Gjennomfør M5 som planlagt (backup og gjenoppretting), og skriv testene først. | **P0** | Beskytter sameiets viktigste data og personopplysninger | E (allerede planlagt) | **Ja** |
| F-02 | **Programmet krever .NET SDK og kommando for å starte.** Dobbeltklikk-demoen er bare for utvikling. [K] | `scripts/`, M5C | Gjennomfør M5C (selvstendig Windows-pakke) før noen uten IT-bakgrunn skal bruke det. | **P0** | Uten dette kan ikke et styremedlem starte programmet selv | E (planlagt) | **Ja** |
| F-17 | **Førstegangsbruk viser bare nuller.** Med tom database sier dashboardet «0, 0, 0, 0» og tomme lister, uten noen hint om hvor man begynner. Tomtilstandene er ellers gode (hver liste forklarer hva man gjør). [N] | `Pages/Dashboard.razor` | Vis en kort «Kom i gang»-tekst bare når databasen er helt tom (1. Registrer boenhetene, 2. …). | P1 | Et nytt sameie ser straks hva første steg er | E (liten tekst) | Ja |
| F-18 | **Bare én lokal bruker og ingen innlogging** (bevisst for v1). Alle som kan åpne maskinen kan se kontaktopplysninger. [K] | `CLAUDE.md` (avgrensning) | Ingen endring i programmet. Skriv i brukerveiledningen at maskinen bør ha vanlig passord/skjermlås og at sikkerhetskopier lagres trygt. | P2 | Realistiske forventninger til personvern | E (dokumentasjon) | Ja (tekst) |
| F-19 | **Ingen endringshistorikk.** Programmet viser *når* noe sist ble endret, men ikke *hva* eller *hvem*. For sameiesaker og vedtak kan det være ønskelig å se hva som tidligere stod. [K] | Domene (`UpdatedAt`) | **Ikke bygg dette nå.** Spør eieren om styret faktisk trenger det. Løser det et konkret problem (for eksempel tvist om et vedtak), kan korrigerte vedtak vises med tidligere tekst. | P3 | – | N | Nei |
| F-20 | **Navigasjon og språk er stort sett konsekvent.** Fem menyvalg i logisk rekkefølge (Dashboard, Boenheter, Styresaker, Oppgaver, Elbillading), «Registrer …» overalt, norske statusnavn og norske datoer. Små avvik: lenketekster som «Åpne», «Se avstemming», «Se laderen og avlesninger», «Avlesningshistorikk» bruker ulike verb. [N] | Alle sider | Bruk samme mønster, for eksempel «Åpne» for å åpne en side. | P3 | Litt mindre grubling | E | Nei |
| F-21 | **Sameiets navn finnes ingen steder i programmet.** Toppen sier bare «SameieHub». For en som bruker programmet for ett sameie går det bra. For skjermbilder, utskrift eller flere installasjoner er det uklart hvilket sameie man ser på. [K] | `Layout/NavMenu.razor`, ingen modell for sameiets navn | Ikke gjør noe nå. Vurder et felt for sameiets navn bare hvis flere sameier faktisk skal bruke programmet (se del 5). | P3 | – | N | Nei |

### Dashboard

| ID | Funn | Hvor | Anbefaling | P | Nytte | Type | Før første bruk? |
|---|---|---|---|---|---|---|---|
| F-08 | **Dashboardet er oversiktlig og viser det viktigste først:** fire tall, oppgaver som krever oppfølging (forfalt først), åpne styresaker, elbillading og kommende datoer. Tallene er forståelige, og «Forfalte oppgaver» er rødt og lenker til filtrert liste. [N] | `/` | Ingen endring. | – | – | – | – |
| F-09 | **Den hyppigste elbilhandlingen, «Registrer avlesning», finnes ikke på dashboardet.** Hurtighandlingene er Boenhet, Styresak og Oppgave, men avlesning (månedlig) krever to klikk via menyen. Seksjonen «Elbillading» har bare «Se elbillading». [N] | `Pages/Charging/DashboardChargingSection.razor`, `Pages/Dashboard.razor` | Legg lenken «Registrer avlesning» i den eksisterende elbillingsseksjonen. | P2 | Sparer et klikk for en oppgave som gjentas hver måned | E | Nei |
| F-22 | **Møtedato vises to ganger:** i «Aktuelle styresaker» og i «Kommende datoer» (som styremøte). Det er forståelig, men gir litt gjentakelse. [N] | `Pages/Dashboard.razor` | La være. Gjentakelsen hjelper en som leser bare én av dem. | P3 | – | E | Nei |

### Boenheter

| ID | Funn | Hvor | Anbefaling | P | Nytte | Type | Før første bruk? |
|---|---|---|---|---|---|---|---|
| F-11 | **Det er lett å finne riktig bolig** (åtte rader, søk på adresse, eier, bolignummer og seksjon, filter Aktive/Inaktive/Alle). Bare adresse er obligatorisk ved registrering. Kontaktinformasjon er samlet fornuftig per eier, og telefon og e-post er lenker. [N] | `/boenheter`, `/boenheter/{id}` | Ingen endring. | – | – | – | – |
| F-12 | **Forskjellen på «Deaktiver» og «Slett» forklares for sent.** Slett har tydelig tekst og bekreftelsesdialog («Dette kan ikke angres»). «Deaktiver» har ingen forklaring før man har trykket; da står det at inaktive boenheter beholdes og kan reaktiveres. [N][K] | `Pages/HousingUnits/HousingUnitDetail.razor` (knappen «Deaktiver», avsnittet «Slett boenhet») | Legg til en kort tekst ved knappen: «Skjuler boenheten fra listene, men beholder alle opplysninger.» | P1 | Hindrer at noen sletter en bolig når de egentlig vil «fjerne» en solgt bolig | E (tekst) | Ja |
| F-13 | **«ID 1» vises på detaljsiden.** Databasens interne nummer betyr ikke noe for brukeren (til forskjell fra «Sak #11», som styret bruker). [N][K] | `HousingUnitDetail.razor` (linje med `ID @_unit.Id`) | Fjern visningen. | P3 | Mindre støy | E | Nei |
| F-14 | **Adresse og bolignummer gjentas i listen** («Fiktivdalen 10A» og «10A» under). Bolignummer kan ha egen verdi i et ekte sameie, så feltet skal ikke fjernes. [N][K] | `HousingUnitList.razor` (visning av `UnitNumber`) | Vis bolignummer bare hvis det ikke allerede står i adressen. | P3 | Litt mindre rot | E | Nei |
| F-15 | **«Bolignummer» og «Seksjonsnummer» forklares ikke.** Eksemplet «For eksempel H0101» passer ikke alle, og seksjonsnummer har ingen hjelpetekst. [N] | `HousingUnitForm.razor` | Kort hjelpetekst («Seksjonsnummer står i seksjoneringen/tinglyst»). | P3 | Færre feil i registeret | E | Nei |

### Styresaker

| ID | Funn | Hvor | Anbefaling | P | Nytte | Type | Før første bruk? |
|---|---|---|---|---|---|---|---|
| F-03 | **Listen viser ikke møtedato**, men «Opprettet» og «Sist oppdatert» med klokkeslett (fra 992 px bredde). Møtedato er det styret styrer etter, og står bare på dashboardet og detaljsiden. [N][K] | `Pages/BoardCases/BoardCaseList.razor` (kolonnene `Opprettet`, `Sist oppdatert`) | Bytt de to tidsstemplene med «Møtedato» (og behold tidsstemplene på detaljsiden). | P1 | Styret ser straks hvilke saker som skal opp på neste møte | E | Ja |
| F-04 | **Handlinger-kolonnen er skjult.** Ved ca. 1024 px er tabellen bredere enn rommet (801 mot 671 px), så «Åpne/Rediger» ligger utenfor. På mobil (487 mot 342 px) må hele listen rulles sideveis. Tittelen er allerede en lenke, så knappene er delvis overflødige. [N] | `BoardCaseList.razor` | Fjern knappkolonnen (tittelen åpner saken) eller skjul «Opprettet». | P1 | Alt viktig er synlig uten sideveis rulling | E | Ja |
| F-23 | **Status og prioritet er forståelige** (Ny, Under behandling, Vedtatt, Avsluttet; Høy/Normal/Lav). Regelen om at «Vedtatt» krever registrert vedtak står på siden og avvises med norsk melding. Koblinger til oppgaver og boenhet er lett å finne: oppgavene står på saken, og «Registrer oppgave» ligger der. [N] | `/styresaker/{id}` | Ingen endring. | – | – | – | – |

### Oppgaver

| ID | Funn | Hvor | Anbefaling | P | Nytte | Type | Før første bruk? |
|---|---|---|---|---|---|---|---|
| F-05 | **«Ansvarlig» og «Handlinger» ligger utenfor synsfeltet** ved 1024 px (tabell 898 mot 671 px). Ansvarlig er det en oppgaveliste først og fremst skal vise. Mobil: 429 mot 342 px. [N] | `Pages/TaskItems/TaskList.razor` | Vis Ansvarlig i stedet for Kategori på smal skjerm, og fjern knappkolonnen (tittelen er lenke). | P1 | Man ser hvem som skal gjøre hva uten å rulle | E | Ja |
| F-06 | **Mange filtre.** Det er søk, seks nedtrekkslister og en avkrysning (status, kategori, prioritet, boenhet, styresak, sortering, «Bare forfalte»). På mobil fyller de hele første skjermbilde før første oppgave vises. [N] | `TaskList.razor` | Vis søk, status og «Bare forfalte» som standard, og legg resten bak «Flere filtre». | P2 | Raskere å nå selve listen, særlig på mobil | E | Nei |
| F-07 | **Å fullføre en oppgave er skjult.** «Marker som fullført» ligger nederst på detaljsiden (omtrent 700 px nede). Bekreftelsen «Oppgaven er markert som fullført» vises øverst og er utenfor skjermen når man har rullet ned, så det ser ut som ingenting skjedde. (Det virker: status, fullføringstidspunkt og «Gjenåpne oppgaven» oppdateres.) Det finnes ingen hurtigknapp i listen. [N] | `Pages/TaskItems/TaskDetail.razor` | Flytt «Marker som fullført» / «Gjenåpne» til sidens overskrift ved siden av «Rediger», eller vis meldingen ved knappen. | P1 | Å fullføre og gjenåpne er den vanligste handlingen, og brukeren får tydelig tilbakemelding | E | Ja |
| F-24 | **Frist, status og ansvarlig er synlige på detaljsiden,** og forfalt vises både med tekst («Forfalt») og farge. [N] | `/oppgaver/{id}` | Ingen endring. | – | – | – | – |

### Elbillading

| ID | Funn | Hvor | Anbefaling | P | Nytte | Type | Før første bruk? |
|---|---|---|---|---|---|---|---|
| F-10 | **Avstemmingen forklarer ikke hva differansen betyr.** Siden viser «+54,5 %» (i demoen) uten forklaring på hva som er normalt, at differansen ikke nødvendigvis er tap, eller hva man bør gjøre. Ordene «dokumentert» og «estimeres» er fagspråk. [N][K] | `Pages/Charging/ReconciliationView.razor` og `ReconciliationPage.razor` | Legg til to–tre linjer hjelpetekst: «Differansen er strømmen hovedmåleren målte utover undermålerne (for eksempel fellesforbruk og unøyaktige målere). Den er ikke nødvendigvis tap.» Bruk «avlest» i stedet for «dokumentert». | P1 | Styret forstår tallet og unngår feil konklusjoner | E (tekst) | Ja |
| F-25 | **Forskjellen på «ikke avlest» og 0 kWh er tydelig.** Skjemaet sier «La feltet stå tomt hvis måleren ikke ble avlest. Tomt er ikke det samme som 0.» Bekreftelsen viser «Ikke avlest», og avstemmingen sier «mangler avlesning» og regner ikke manglende verdi som 0. [N] | `ReadingSetCreate.razor`, `ReconciliationView.razor` | Ingen endring. | – | – | – | – |
| F-26 | **«Hovedmåler» betyr to ulike tall.** På oversikten står «Hovedmåler 52793 kWh» (målerstanden, akkumulert), og på avstemmingen «Hovedmåler (forbruk) 330 kWh». Det kan forveksles. [N] | `ChargingIndex.razor`, `ReconciliationView.razor` | Bruk «Målerstand» på oversikten og «Forbruk i perioden» i avstemmingen. | P2 | Unngår misforståelse av et sentralt tall | E (tekst) | Ja |
| F-27 | **Avlesningsskjemaet er enkelt nok i prinsippet:** tid (forhåndsutfylt nå), hovedmåler, fire felt per sikringsskap, forrige verdi og forbruk ved hvert felt, og tydelige feil ved riktig felt. Hjelpeteksten «Norsk tid (klokkeslettet slik det var på stedet)» er snirklete, og valget sommertid/vintertid er forvirrende, men gjelder bare to timer i året. [N] | `ReadingSetCreate.razor`, `ReadingTimeField.razor` | Skriv «Når leste du av målerne?». Behold resten. Få en person i sameiet til å prøve skjemaet foran målerne, siden feltene er ordnet etter adresse og ikke etter merkingen på måleren (se F-28). | P2 | Raskere og tryggere månedlig rutine | E (tekst) | Nei |
| F-28 | **Feltene identifiseres ved boligadresse og lader, men ikke ved merking på selve måleren.** Det er ikke avklart om målerne i skapet er merket med boligens nummer. Hvis ikke, må brukeren vite hvilken måler som hører til hvilken bolig. [K] | `ReadingSetCreate.razor` (`LabelOf`) | **Spør eieren før noe gjøres.** Hvis måleren har en egen merking, kan et valgfritt felt «Merking» være aktuelt (N). Hvis ikke, er dagens løsning tilstrekkelig. | P3 | Færre feilavlesninger hvis måleren ikke er merket med adresse | N (kun hvis problemet bekreftes) | Nei |
| F-29 | **Å rette en feilregistrering er mulig, men ligger langt unna.** Man må gå Elbillading → lader eller hovedmåler → Avlesningshistorikk → Korriger. Etter at et sett er lagret, viser bekreftelsen ingen lenke til å rette det. Feilen man oppdager rett etter innsending krever fire klikk. Selve korrigeringen og slettingen er tydelige, med bekreftelse og kontroll mot nabo-avlesningene. [N] | `ReadingSetCreate.razor` (bekreftelsen), `MeterReadingsPanel.razor` | Legg en lenke til historikken per måler i bekreftelsen («Feil? Rett her»). | P2 | Raskere retting når man oppdager en tastefeil | E | Nei |
| F-30 | **Hovedmåler og undermålere er lette å forstå** på oversikten (tall per status, tabell med skap, status og siste avlesning). Teksten som forklarer anlegget («én felles hovedmåler og én undermåler per lader») står først i oppsettskjemaet. [N] | `/elbillading` | Ingen endring. | – | – | – | – |

### Mindre skjermer

| ID | Funn | Hvor | Anbefaling | P | Nytte | Type | Før første bruk? |
|---|---|---|---|---|---|---|---|
| F-31 | **Ingen side har sideveis rulling på mobil (375 px)**, og menyen blir en knapp. Det er bra. Likevel må tabellene i oppgaver (429/342 px), styresaker (487/342 px), boenheter (344/342 px), ladedetalj og avlesningshistorikk (426/294 px) rulles sideveis inne i seg selv. Filtrene fyller første skjermbilde i oppgavelisten. [N] | Listesidene | Løses i hovedsak av F-04, F-05 og F-06. | P2 | Mobil og nettbrett blir brukbare for styremedlemmer som sjekker saker underveis | E | Nei |
| F-32 | **Skjemaene fungerer på mobil** (felt på full bredde, store trykkflater, ingen avkutting). [N] | `…/ny`, `…/avlesning` | Ingen endring. | – | – | – | – |

## 4. Hva jeg anbefaler å gjøre først

**Før første reelle bruk (nødvendig):**
1. **F-01 Backup (M5)** og **F-02 enkel oppstart (M5C)**. Uten dem er programmet ikke trygt eller mulig å bruke for andre enn deg.

**Før første reelle bruk (små justeringer som gir mest, i denne rekkefølgen):**
2. **F-07** Flytt «Marker som fullført» opp og gjør tilbakemeldingen synlig.
3. **F-04, F-05, F-03** Gjør listene bredde-vennlige: fjern knappkolonnene, vis Ansvarlig og Møtedato.
4. **F-10, F-26** Forklar differansen og rett de to «Hovedmåler»-etikettene.
5. **F-12** Forklar «Deaktiver».
6. **F-17** «Kom i gang»-tekst når databasen er tom.

**Etter første bruk, basert på hva brukerne faktisk sier:** F-06, F-29, F-09, F-27 og de øvrige P3-punktene.

**Anbefaling om fremgangsmåte:** la én til to personer fra sameiet prøve fem oppgaver med den fiktive demoen (for eksempel «finn ut hvem som skal gjøre noe med lekkasjen», «registrer en avlesning», «fullfør en oppgave», «finn neste styremøte», «rett en feil avlesning») uten hjelp. Det som tar tid eller feiler der, er det som skal prioriteres, mer enn denne listen.

**Spørsmål til eieren (ikke noe å bygge før svar finnes):**
- Skal styret kunne skrive ut en saksliste eller avstemming til sameiemøtet? Hvis ja, hva må stå på papiret? (Ikke vurdert fordi det ikke er et krav i dag.)
- Er målerne i sikringsskapene merket med boligens nummer? (F-28)
- Trenger styret å se hva et vedtak tidligere stod som? (F-19)
- Skal ladestrøm fordeles på husstandene som kostnad? Ifølge `CLAUDE.md` er økonomi utenfor v1.0, så det er ikke foreslått.

## 5. Antakelser som bare passer det første sameiet (kildekodegjennomgang)

Skillet er: **(1) demodata** (faste verdier er greit), **(2) bevisste regler for første versjon** (riktig nå, men begrenser flere sameier), og **(3) uheldig hardkoding** (begrenser videreutvikling uten at det er nødvendig). **Ingenting her er endret, og jeg anbefaler ikke å endre domenemodellen nå.**

### 5.1 Demodata (helt greit)
- Adressene «Fiktivdalen 10A–16B» (`DemoDatabaseSeeder.AddressPrefix`, `UnitNumbers`), åtte boliger, fordeling på sikringsskap og alle verdier i `DemoChargingPlan`, demoportene og demomappen `SameieHubDemo`.
- Disse brukes bare av demoseederen, og seederen kjører bare mot demodatabasen.

### 5.2 Bevisste regler for første versjon

| Regel | Hvor | Merknad |
|---|---|---|
| Én lokal bruker, ingen innlogging | `CLAUDE.md`, ingen autentisering | Bevisst avgrensning |
| **Ett sameie per database.** Det finnes høyst ett ladeanlegg (databasen har `CHECK ("Id" = 1)`), og modellen har ingen «sameie»-enhet | `SameieHubDbContext.cs`, `ChargingFacility.cs` | Flere sameier kan senere løses med én databasefil per sameie, uten å endre modellen (se 5.4) |
| Én lader per boenhet (unik indeks) | `EvCharger`, `IEvChargerRepository` | Passer første sameie. Delte ladeplasser støttes ikke |
| Én hovedmåler med undermålere under | `Meter.cs` | Passer sameiets oppsett |
| Norsk tid (Europe/Oslo) og norsk språk | `NorwegianTime.cs`, Razor-tekster | Bevisst. Ingen oversettelse |
| Avlesning, toleranse 24 timer, og forbruk regnet av differanser | `MeterHistory.cs` | Bevisst og dokumentert |
| Dashboardets vinduer (7 dager oppfølging, 60 dager kommende, maks 8 rader) | `DashboardOverview.cs` | Faste, men fornuftige |
| Databasen ligger i `%LOCALAPPDATA%\SameieHub\sameiehub.db` | `DatabaseLocation.cs` | `Database:Path` kan overstyre |
| Boenhetens felt: seksjonsnummer 1–9999, areal høyst 1000 m² | `HousingUnit.cs` | Romslig |

### 5.3 Uheldig hardkoding (begrenser videreutvikling)

Alle punktene under er **harmløse for det første sameiet** (som har akkurat åtte ladere i to skap). De er listet fordi de ville hindret et annet sameie.

| ID | Hardkoding | Hvor | Hva den begrenser | Foreslått tilnærming (ikke gjort) |
|---|---|---|---|---|
| H-1 | **Avstemming er «fullstendig» bare med nøyaktig åtte undermålere** (`ChargingFacility.ExpectedSubMeterCount = 8`). Et sameie med seks eller ti ladere kan aldri få en fullstendig avstemming. Selve avstemmingsmotoren tar antallet som parameter, men tjenesten sender alltid konstanten. | `Domain/ChargingFacility.cs:17`, `ChargingReconciliation.cs` (parameter `expectedSubMeters`), `ChargingService.ReconcileAsync` | Sameier med annet antall ladere | Lagre forventet antall på anlegget (eller bruk antall registrerte ladere). Krever domene- og lagringsendring, så vent til et annet sameie finnes. |
| H-2 | **To sikringsskap med fire undermålere i hvert** er innebygd: `Meter.MinCabinetNumber/MaxCabinetNumber` (1–2), en `CHECK` i databasen, `ReadingSetCreate.ExpectedPerCabinet = 4`, og valgene «Sikringsskap 1/2» i `ChargerForm`. Teksten på oppsettssiden sier også «åtte undermålere, fire i hvert av to sikringsskap». | `Meter.cs`, `ReadingSetCreate.razor`, `ChargerForm.razor`, `ChargingIndex.razor`, migreringen | Annet antall skap eller målere per skap | Gjør skapnummer til et vanlig positivt tall uten fast øvre grense, og fjern «fire per skap» fra UI. |
| H-3 | **Ingen «bygning»-nivå.** De fire tomannsboligene finnes ikke som egne enheter; 10A og 10B er to uavhengige boenheter. Oppgaver og styresaker kan knyttes til én boenhet eller til hele sameiet, men ikke til «bygning 12». | `HousingUnit`, `TaskItem`, `BoardCase` | Arbeid som gjelder en hel tomannsbolig (tak, fasade) | **Ikke bygg.** Spør først om dette er et reelt problem. I dag kan oppgaven knyttes til hele sameiet og beskrives i teksten. |
| H-4 | **Sameiets navn finnes ikke** i modellen eller grensesnittet. | `NavMenu.razor` | Å skille flere sameier (se F-21) | Vurder bare hvis flere sameier skal bruke programmet. |
| H-5 | **«Sikringsskap», «Elbillader», «Tomannsbolig» som faste begreper** i UI-tekster. | `ChargingText.cs`, `HousingTypeText.cs` | Ingen reell begrensning (andre sameier bruker de samme begrepene) | Ingen. |

### 5.4 Om flere sameier

Det enkleste og tryggeste er at **hvert sameie får sin egen databasefil og sin egen installasjon** (programmet støtter allerede `Database:Path`). Da trengs ingen «sameie»-modell, ingen innlogging per sameie og ingen endring av domenet. Det er bare H-1 og H-2 som må rettes, og først når et annet sameie faktisk har et annet oppsett. Jeg anbefaler å **ikke** endre modellen i forkant.

### 5.5 Det som ikke er hardkoding
- Boligtype (`HousingType`), seksjonsnummer, bolignummer og eiere er generelle og ikke knyttet til demodataene.
- Antallet boenheter, styresaker og oppgaver har ingen øvre grense.
- Demoseederen er isolert fra vanlig oppstart og ordinær database (testet).

## 6. Oppstartstest og dependency injection (Del A)

**Problemet:** `Program.cs` var ikke testet. Siden sidene ikke rendres på serveren (uten prerendering), ville en manglende `AddScoped<…>` først vist seg når en person åpnet en side. Testene bygger sine egne containere, så de ville ikke oppdaget det.

**Løsning:** `StartupTests` (`tests/SameieHub.Tests/Startup/StartupTests.cs`) starter det ekte programmet med `WebApplicationFactory<Program>` mot en midlertidig SQLite-fil i systemets temp-mappe, med miljøvariabelen `Database__Path` satt på samme måte som ved vanlig oppstart.

**Hvorfor `Microsoft.AspNetCore.Mvc.Testing` og ikke en prosesstest:**
- Den gir tilgang til **programmets egen DI-container** (`Services`). Det trengs for å bevise at tjenestene er registrert. En prosesstest ville bare kunne hente `/`, som returnerer en tom skallside uten å bruke noen tjeneste.
- Den kjører om lag ett sekund, uten porter, ventetid eller drepte prosesser.
- Den er den vanlige og offisielle måten å teste ASP.NET Core-oppstart på.
- **Kostnad:** én ny testavhengighet (`Microsoft.AspNetCore.Mvc.Testing` 10.0.15, bare i testprosjektet) og én ikke-funksjonell linje i `SameieHub.Web.csproj` (`InternalsVisibleTo` for testprosjektet, slik at testen får tilgang til den implisitte `Program`-klassen). Ingen programkode er endret.
- Prosesstest ble vurdert og ikke valgt fordi den ikke kan kontrollere DI.

**Testene (10 kjørte tester):**

| Test | Hva den viser |
|---|---|
| `Startup_UsesTheTemporaryDatabaseAndNotTheOrdinaryOne` | Tilkoblingen peker på den midlertidige filen og ikke på AppData-databasen, og filen er opprettet |
| `Startup_AppliesAllMigrations` | Ingen ventende migreringer, alle anvendt |
| `NormalStartup_CreatesNoDemoOrOtherData` | Alle sju tabeller er tomme etter oppstart |
| `Startup_RegistersTheServicesAndRepositoriesTheProgramNeeds` | Alle fire tjenester, `TimeProvider` og sju repositories finnes (containeren validerer også scopes) |
| `Startup_EveryServiceInjectedIntoAPageCanBeResolved` | Finner alle `@inject`-tjenester i alle komponenter ved refleksjon og løser dem fra den ekte containeren. Fanger en glemt registrering selv om siden bare lastes i nettleseren |
| `Get_Page_ReturnsTheApplicationShell` (`/` og `/elbillading`) | 200, HTML med `blazor.web.js` og `lang="nb"` |
| `Get_UnknownAddress_DoesNotBreakTheApplication` | Ukjent adresse gir ikke 500 |
| `Startup_NeverTouchesTheOrdinaryDatabase` | Den ordinære databasefilen har samme størrelse og tidspunkt etter oppstart og forespørsler (filen leses ikke, bare dens metadata) |
| `Cleanup_StoppingTheApplicationDeletesTheTemporaryDatabaseAndRestoresTheSetting` | Når applikasjonen stoppes, slettes den midlertidige mappen og miljøvariabelen settes tilbake |

**Kontrollert at testene feiler riktig:** da jeg midlertidig fjernet `AddScoped<ChargingService>()` fra `Program.cs`, feilet to tester med meldingen «ChargingService is injected into a component but is not registered». Endringen ble tilbakeført.

**Begrensning:** testen setter en prosessbred miljøvariabel mens den kjører. Ingen andre tester bruker `Database__Path`, men en fremtidig oppstartstest i samme prosess må ta hensyn til det (dokumentert i koden).
