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

**Gjenstår (senere):** eksport av data og backup/gjenoppretting (M5). Blokkeringen ved sletting av boenhet er testet mot styresaker (ekte fremmednøkkel). Mot oppgaver testes den når `TaskItem` finnes (M3).

### Styresaker (M2A: domenemodell)
En styresak har tittel (obligatorisk), beskrivelse, status (Ny, Under behandling, Vedtatt, Avsluttet), prioritet (Lav, Normal, Høy), opprettelsestidspunkt, møtedato, vedtak, merknader og en valgfri kobling til én boenhet (ingen kobling = gjelder hele sameiet). Regler og begrunnelser står i [ARCHITECTURE.md](ARCHITECTURE.md). Repository og tjeneste (M2B) er på plass: styresaker kan registreres, endres, få status og vedtak, kobles til en aktiv boenhet og slettes (sletting blokkeres senere når oppgaver refererer til saken). `UpdatedAt` viser når saken sist ble endret. Styresaker lagres i SQLite (M2C) med en fremmednøkkel til boenheter som aldri sletter eller nullstiller noe automatisk. Full endringshistorikk er ikke implementert; bare opprettelses- og endringstidspunkt. Brukergrensesnitt (M2D): oversikt med søk, filtre og sortering, registrering, redigering, detaljside med statusendring og vedtak, sletting med bekreftelse, og en seksjon med tilknyttede styresaker på boenhetens side. Statuser vises som Ny, Under behandling, Vedtatt og Avsluttet; prioritet som Lav, Normal og Høy.

## Ikke-funksjonelle krav
- Kjører lokalt på Windows uten Visual Studio, .NET SDK eller internett (selvstendig publisering, M5).
- Én lokal bruker; ingen innlogging.
- Backup og gjenoppretting av SQLite-data er obligatorisk før v1.0 godkjennes.
- Data forblir lokalt og committes aldri.

## Utenfor v1.0
Økonomi og felleskostnader · flere brukere/innlogging · dokumentarkiv · ekstern tilgang · flere sameier · eksterne integrasjoner.
