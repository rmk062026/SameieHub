# Krav

## Formål
Et enkelt, lokalt program for administrasjon av et lite sameie med selveide boliger i tomannsboliger, egne husnummer, private uteområder og fellesarealer.

## Funksjonsomfang v1.0
1. **Dashboard** – samlet oversikt (åpne styresaker, forfalte/kommende oppgaver).
2. **Boenheter** (`HousingUnit`) – register over boliger og eiere/kontaktinfo.
3. **Styresaker** (`BoardCase`) – saker med status, beskrivelse og vedtak.
4. **Oppgaver og vedlikehold** (`TaskItem`) – oppgaver med frist, status og kategori (vedlikehold er en kategori).

Detaljerte felter og relasjoner fastsettes i M1–M3 (se «Datamodell» i [ARCHITECTURE.md](ARCHITECTURE.md)).

## Ikke-funksjonelle krav
- Kjører lokalt på Windows uten Visual Studio, .NET SDK eller internett (selvstendig publisering, M5).
- Én lokal bruker; ingen innlogging.
- Backup og gjenoppretting av SQLite-data er obligatorisk før v1.0 godkjennes.
- Data forblir lokalt og committes aldri.

## Utenfor v1.0
Økonomi og felleskostnader · flere brukere/innlogging · dokumentarkiv · ekstern tilgang · flere sameier · eksterne integrasjoner.
