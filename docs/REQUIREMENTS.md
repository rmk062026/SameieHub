# Krav

## Formål
Et enkelt, lokalt program for administrasjon av et lite sameie med selveide boliger i tomannsboliger, egne husnummer, private uteområder og fellesarealer.

## Funksjonsomfang v1.0
1. **Dashboard** – samlet oversikt (åpne styresaker, forfalte/kommende oppgaver).
2. **Boenheter** (`HousingUnit`) – register over boliger og eiere/kontaktinfo.
3. **Styresaker** (`BoardCase`) – saker med status, beskrivelse og vedtak.
4. **Oppgaver og vedlikehold** (`TaskItem`) – oppgaver med frist, status og kategori (vedlikehold er en kategori).

Detaljerte felter og relasjoner fastsettes i M1–M3 (se «Datamodell» i [ARCHITECTURE.md](ARCHITECTURE.md)).

### Boenheter – status etter M1D
Ferdig: registrere, vise (liste med antall) og endre adresse, lagret i SQLite.

**Gjenstår før hele M1 kan godkjennes** (foreslått som M1E, hvert punkt avklares før koding):
1. **Eier og kontaktinformasjon:** nye felter i `HousingUnit` (f.eks. eiernavn, e-post, telefon). Avklar hvilke som er obligatoriske, valideringsregler og personvern (data er personopplysninger og lagres bare lokalt).
2. **Identifikasjon av boligen:** husnummer/bokstav som eget felt, i stedet for å legge alt i `Address`, hvis det er nødvendig for sortering og visning.
3. **Sletting eller deaktivering:** avklar først kontrakten (`IHousingUnitRepository`, `HousingUnitService`) og hva som skjer med senere koblinger fra styresaker og oppgaver. Foreslått: deaktivering fremfor hard sletting.
4. **Søk/filter i listen** hvis antall boenheter gjør det nyttig.
5. Ny migrering, repository/service-tester og UI-tester for hvert nytt felt.

## Ikke-funksjonelle krav
- Kjører lokalt på Windows uten Visual Studio, .NET SDK eller internett (selvstendig publisering, M5).
- Én lokal bruker; ingen innlogging.
- Backup og gjenoppretting av SQLite-data er obligatorisk før v1.0 godkjennes.
- Data forblir lokalt og committes aldri.

## Utenfor v1.0
Økonomi og felleskostnader · flere brukere/innlogging · dokumentarkiv · ekstern tilgang · flere sameier · eksterne integrasjoner.
