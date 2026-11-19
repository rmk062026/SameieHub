# Krav

## Formål
Et enkelt, lokalt program for administrasjon av et lite sameie med selveide boliger i tomannsboliger, egne husnummer, private uteområder og fellesarealer.

## Funksjonsomfang v1.0
1. **Dashboard** – samlet oversikt (åpne styresaker, forfalte/kommende oppgaver).
2. **Boenheter** (`HousingUnit`) – register over boliger og eiere/kontaktinfo.
3. **Styresaker** (`BoardCase`) – saker med status, beskrivelse og vedtak.
4. **Oppgaver og vedlikehold** (`TaskItem`) – oppgaver med frist, status og kategori (vedlikehold er en kategori).

Detaljerte felter og relasjoner fastsettes i M1–M3 (se «Datamodell» i [ARCHITECTURE.md](ARCHITECTURE.md)).

### Boenheter (M1E)
Registrere, vise, redigere, søke etter og deaktivere/reaktivere boenheter. Bare adressen er obligatorisk.

| Opplysning | Regel |
|---|---|
| Adresse | Obligatorisk, maks 200 tegn |
| Bolignummer (f.eks. H0101) | Valgfritt, maks 20 tegn |
| Seksjonsnummer | Valgfritt heltall 1–9999 |
| Boligtype | Valgfri: Tomannsbolig, Enebolig, Rekkehus, Leilighet, Annet |
| Areal | Valgfritt, større enn 0 og høyst 1000 m² |
| Eier 1 og eier 2: navn, telefon, e-post | Valgfritt. Navn maks 100 tegn, telefon 5–30 tegn (tall, mellomrom, + - og parenteser, minst 5 siffer), e-post maks 254 tegn og gyldig adresse |
| Merknader | Valgfritt, maks 2000 tegn |
| Aktiv/inaktiv | Standard aktiv |

**Begrensninger**
- En boenhet har **maksimalt to eiere**, som to enkle sett med felter. Det finnes ikke noe personregister, eierandeler eller eierskifter. Ved eierskifte endres feltene.
- **Deaktivering** beholder alle data og kan angres. **Permanent sletting** (M1F) fjerner boenheten og alle opplysninger om den, inkludert eier- og kontaktinformasjon, og kan ikke angres. Den krever bekreftelse, og blokkeres hvis styresaker eller oppgaver refererer til boenheten. Brukes for feilregistrerte boenheter.

**Personvern.** Eiernavn, telefon og e-post er personopplysninger. De lagres bare i den lokale databasen, sendes ikke til eksterne tjenester, skrives ikke i logger, og testene bruker bare fiktive data. SameieHub er et lokalt register; praktisk bruk må følge gjeldende personvernkrav (f.eks. formål, tilgang og sletting av opplysninger som ikke lenger trengs).

**Gjenstår (senere):** eksport av data og backup/gjenoppretting (M5). Blokkeringen ved sletting av boenhet er testet mot styresaker (ekte fremmednøkkel). Mot oppgaver er den også testet med ekte fremmednøkkel (M3C).

### Styresaker (M2A: domenemodell)
En styresak har tittel (obligatorisk), beskrivelse, status (Ny, Under behandling, Vedtatt, Avsluttet), prioritet (Lav, Normal, Høy), opprettelsestidspunkt, møtedato, vedtak, merknader og en valgfri kobling til én boenhet (ingen kobling = gjelder hele sameiet). Regler og begrunnelser står i [ARCHITECTURE.md](ARCHITECTURE.md). Repository og tjeneste (M2B) er på plass: styresaker kan registreres, endres, få status og vedtak, kobles til en aktiv boenhet og slettes (sletting blokkeres senere når oppgaver refererer til saken). `UpdatedAt` viser når saken sist ble endret. Styresaker lagres i SQLite (M2C) med en fremmednøkkel til boenheter som aldri sletter eller nullstiller noe automatisk. Full endringshistorikk er ikke implementert; bare opprettelses- og endringstidspunkt. Vedtak kan ha en valgfri **vedtaksdato** (når styret fattet vedtaket, M2E). Datoen kan bare finnes sammen med et vedtak, kan stå tom og kan korrigeres, og den fjernes når vedtaket fjernes. Fra en aktiv boenhet kan man registrere en styresak med boenheten forhåndsvalgt. Opprettelses- og endringstidspunktet viser ikke hvilke endringer som er gjort; full endringshistorikk er ikke implementert og må vurderes hvis SameieHub skal brukes til formell protokollføring. Brukergrensesnitt (M2D): oversikt med søk, filtre og sortering, registrering, redigering, detaljside med statusendring og vedtak, sletting med bekreftelse, og en seksjon med tilknyttede styresaker på boenhetens side. Statuser vises som Ny, Under behandling, Vedtatt og Avsluttet; prioritet som Lav, Normal og Høy.

### Dashboard (M4)
Startsiden viser reelle tall fra databasen: aktive boenheter, åpne styresaker, åpne oppgaver og forfalte oppgaver (hvert tall er en lenke til riktig modul), oppgaver som krever oppfølging, aktuelle styresaker, kommende møter og frister, og hurtighandlinger. «Åpen styresak» er en sak som ikke er avsluttet, og «åpen oppgave» er en oppgave som ikke er fullført. Forfall regnes mot norsk dato. Dashboardet kan oppdateres uten omstart og endrer aldri data. Detaljer står i [ARCHITECTURE.md](ARCHITECTURE.md) og [UI_DESIGN.md](UI_DESIGN.md).

### Oppgaver og vedlikehold (M3A–M3D)
En oppgave har tittel (obligatorisk), beskrivelse, kategori (Generell eller Vedlikehold), status (Ikke startet, Pågår, Fullført), prioritet (Lav, Normal, Høy), frist (dato), ansvarlig (navn), valgfri kobling til én boenhet og/eller én styresak, merknader, opprettet, sist endret og fullført-tidspunkt. En oppgave er forfalt når den ikke er fullført og fristen er før dagens dato. Regler og begrunnelser står i [ARCHITECTURE.md](ARCHITECTURE.md). Repository og tjeneste (M3B) er på plass: oppgaver kan registreres, endres, få status, kobles til en aktiv boenhet og/eller en eksisterende styresak, slettes, og forfalte oppgaver kan hentes ut (fristen regnes mot norsk dato). Oppgaver lagres i SQLite (M3C): boenhet og styresak med oppgaver kan ikke slettes før koblingene er fjernet. Brukergrensesnittet (M3D) gir oversikt med statistikk, søk, filtre og sortering (standard: ikke fullførte, nærmeste frist først), registrering, redigering, statusendring med «Marker som fullført» og «Gjenåpne», forfalt- og «Frist i dag»-merker, kobling til boenhet og styresak (også fra boenhetens og styresakens side), og permanent sletting med bekreftelse.

## Ikke-funksjonelle krav
- Kjører lokalt på Windows uten Visual Studio, .NET SDK eller internett (selvstendig publisering, M5).
- Én lokal bruker; ingen innlogging.
- Backup og gjenoppretting av SQLite-data er obligatorisk før v1.0 godkjennes.
- Data forblir lokalt og committes aldri.

## Utenfor v1.0
Økonomi og felleskostnader · flere brukere/innlogging · dokumentarkiv · ekstern tilgang · flere sameier · eksterne integrasjoner.
