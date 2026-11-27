# SameieHub – prosjektregler for Claude Code

Lokalt administrasjonsprogram for et lite sameie (selveide boliger i tomannsboliger). Kodehode-innlevering, men også et brukbart produkt og porteføljeprosjekt. Eieren lærer C#/.NET: koden, arkitekturen og testene skal være forståelige og forklares.

## Mål og avgrensninger
- Fire områder i v1.0: Dashboard, Boenheter, Styresaker, Oppgaver (vedlikehold er en kategori av oppgaver).
- Entiteter: `HousingUnit`, `BoardCase`, `TaskItem` (datamodell avklares i `docs/ARCHITECTURE.md` før implementasjon).
- Utenfor v1.0: økonomi, flere brukere/innlogging, dokumentarkiv, ekstern tilgang, flere sameier, eksterne integrasjoner.
- Én lokal bruker, ingen separat API, ingen Docker, ingen skyhosting, ingen avansert autentisering.
- Må kunne kjøres uten Visual Studio, .NET SDK og internett. Backup/gjenoppretting av SQLite er obligatorisk før v1.0.
- Personopplysninger (eiernavn, telefon, e-post): bare i lokal database; aldri i logger, tester, seed-data eller commits. Bruk fiktive testdata.

## Teknologi og arkitektur
- C#, .NET 10, Blazor Web App (Interactive Server), SQLite + EF Core, xUnit, GitHub Actions. Windows lokalt.
- Prosjekter (se `docs/ARCHITECTURE.md`): `Domain` (entiteter, enums, repository-interfaces, forretningsregler; ingen EF/SQLite/Blazor) ← `Infrastructure` (DbContext, repositories, migreringer) ← `Web` (UI, DI, oppstart). `Tests` refererer alle.
- Ingen nye prosjekter, lag, interfaces, abstrakte klasser eller DTO-er uten forklarbar hensikt og godkjenning. Repository Pattern kun når det gir konkret nytte.

## Kodekonvensjoner
- Engelske navn i kode. Nullable reference types, async/await for I/O, korte metoder med ett ansvar.
- `.editorconfig` er gjeldende. Validering og feilhåndtering ved systemgrenser.
- Enkel, idiomatisk og konsekvent C# som en fersk C#-utvikler kan forstå, feilsøke og videreutvikle: tydelige `if`/`else`, `foreach` fremfor kompleks LINQ, små klasser og metoder, klare navn. Ikke kunstig dårlig kode, og ingen overengineering, unødvendige mellomlag eller avansert syntaks for syns skyld.
- `try/catch` kun der feilen faktisk kan håndteres (brukerhandling, filer, database). Ingen tomme `catch` og ingen catch-og-kast-videre uten grunn. Bruk presise unntakstyper og tydelige feilmeldinger.
- Selvforklarende kode fremfor kommentarer. XML-dokumentasjon kun når den tilfører noe signaturen ikke viser (kontrakt, begrensning, sideeffekt, uvanlig oppførsel). Vanlige kommentarer kun for *hvorfor*. Ingen TODO- eller undervisningskommentarer i ferdig kode.
- Undervisning gis i samtalen eller i `docs/`, ikke som kommentarer i C#-filene. Lengre arkitekturforklaringer hører hjemme i `docs/`.
- Terminologi: dette er et sameie (ikke borettslag).

## Test og kvalitet
- TDD for testbar forretningslogikk: test først → bekreft feil av riktig årsak → implementer → grønn → refaktorer. Ingen kunstige commits for å simulere TDD.
- Enhetstester med fake repositories; integrasjonstester mot SQLite. Se `docs/TESTING.md`.
- Før commit: `dotnet build` og `dotnet test` uten feil.

## GitHub
- Repo: `rmk062026/SameieHub`, branch `main`. Før første endring og før hver push: sjekk `git status`, branch, remote, lokal git-identitet (`rmk062026` / `rmk062026@gmail.com`) og `gh api user --jq .login`. Feil konto/remote → STOPP.
- Ikke endre global git-konfig. Aldri force-push.
- Conventional Commits (`feat:`, `fix:`, `test:`, `docs:`, `refactor:`, `chore:`, `ci:`). Hyppige, meningsfulle commits.
- Aldri commit databasefiler, personopplysninger, hemmeligheter, binærfiler eller backuper.

## Dokumentasjon
- Hold `README.md` og `docs/` (REQUIREMENTS, ARCHITECTURE, UI_DESIGN, TESTING, DEVELOPMENT) oppdatert i samme endring som koden. Dokumenter beslutninger med alternativer og begrunnelse. Mermaid for diagrammer.

## Læring (obligatorisk)
- Forklaringer på norsk; kode på engelsk.
- Claude implementerer all kode i godkjent milepæl; ingen studentoppgaver som stopper utviklingen. Eieren lærer gjennom forklaringer, kodegjennomgang og egne øvelser utenfor utviklingsflyten (gis kun på forespørsel). **Unntak: møteadministrasjonen (se under).**
- Etter hver deloppgave: kort forklaring (hva ble laget, hvem har ansvar for hva, hvordan klassene samarbeider, viktige C#-konsepter, hvor i prosjektet koden finnes) med eksempler fra faktisk kode. Ingen lange teoriforelesninger.

## Unntak: møteadministrasjonen (MM0–MM11)
Går foran reglene over om hvem som skriver koden. Plan: `docs/MEETING_MODULE_IMPLEMENTATION_PLAN.md`.
- Prosjekteieren er hovedutvikler og skriver produksjonskoden selv (domene, tjenester, repositories, EF Core, Blazor og tester), med ChatGPT som veileder: teori, avgrensede oppgaver, kodegjennomgang og feilsøking.
- Claude Code kan kartlegge eksisterende kode, skrive dokumentasjon og gjennomføre avtalte kontroller.
- Claude implementerer **ikke** noe i møtemodulen uten uttrykkelig godkjenning.
- Reglene om tester, sikkerhet, kvalitet og GitHub gjelder fortsatt.
- Branch: MM1–MM11 implementeres på `feature/meeting-management` (opprettes først når eieren ber om det). MM0 og dokumentasjonsendringer kan gjøres på `main`.

### Opphav til kode (eier og AI)
Skill mellom selvskrevet kode og AI-generert implementasjon når det er praktisk mulig.
- Kode eieren skriver selv, committes normalt uten AI-medforfatter.
- Kode Claude implementerer, ligger i egne commits og merkes med `Co-Authored-By` der det er reelt medforfatterskap. Den nevnes i kontrollrapporten.
- Kode ChatGPT genererer og eieren kopierer direkte inn, dokumenteres også som AI-bidrag (for eksempel i commit-meldingen).
- Veiledning, forklaringer og vanlig feilsøkingshjelp gjør ikke i seg selv AI til medforfatter.

## Milepælbasert arbeid
- Jobb kun innen godkjent milepæl; STOPP ved slutten og vent på uttrykkelig godkjenning før neste. Avslutt hver milepæl med kontrollrapport (struktur, endrede filer, build/test-resultater, CI-status lokalt vs. GitHub, commits, feil, begreper, kontrollspørsmål).
- M0 fundament (gjort) · M1 Boenheter · M2 Styresaker · M3 Oppgaver/vedlikehold · M4 Dashboard · M5 Backup/gjenoppretting og pakking for lokal kjøring. Planen kan justeres med eierens godkjenning.
