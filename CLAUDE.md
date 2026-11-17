# SameieHub – prosjektregler for Claude Code

Lokalt administrasjonsprogram for et lite sameie (selveide boliger i tomannsboliger). Kodehode-innlevering, men også et brukbart produkt og porteføljeprosjekt. Eieren lærer C#/.NET: koden, arkitekturen og testene skal være forståelige og forklares.

## Mål og avgrensninger
- Fire områder i v1.0: Dashboard, Boenheter, Styresaker, Oppgaver (vedlikehold er en kategori av oppgaver).
- Entiteter: `HousingUnit`, `BoardCase`, `TaskItem` (datamodell avklares i `docs/ARCHITECTURE.md` før implementasjon).
- Utenfor v1.0: økonomi, flere brukere/innlogging, dokumentarkiv, ekstern tilgang, flere sameier, eksterne integrasjoner.
- Én lokal bruker, ingen separat API, ingen Docker, ingen skyhosting, ingen avansert autentisering.
- Må kunne kjøres uten Visual Studio, .NET SDK og internett. Backup/gjenoppretting av SQLite er obligatorisk før v1.0.

## Teknologi og arkitektur
- C#, .NET 10, Blazor Web App (Interactive Server), SQLite + EF Core, xUnit, GitHub Actions. Windows lokalt.
- Prosjekter (se `docs/ARCHITECTURE.md`): `Domain` (entiteter, enums, repository-interfaces, forretningsregler; ingen EF/SQLite/Blazor) ← `Infrastructure` (DbContext, repositories, migreringer) ← `Web` (UI, DI, oppstart). `Tests` refererer alle.
- Ingen nye prosjekter, lag, interfaces, abstrakte klasser eller DTO-er uten forklarbar hensikt og godkjenning. Repository Pattern kun når det gir konkret nytte.

## Kodekonvensjoner
- Engelske navn i kode. Nullable reference types, async/await for I/O, korte metoder med ett ansvar.
- `.editorconfig` er gjeldende. Validering og feilhåndtering ved systemgrenser.
- Enkel, idiomatisk og konsekvent C# som prosjekteieren (en student) kan forstå, feilsøke og videreutvikle. Ingen overengineering, unødvendige mellomlag eller avansert syntaks for syns skyld.
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
- Når nye C#/.NET-konsepter introduseres: hva det er, hvorfor vi bruker det, hvordan det samspiller med andre klasser, kort eksempel fra faktisk kode, hvordan det testes, gjerne en liten øvelse. Ingen lange teoriforelesninger.

## Milepælbasert arbeid
- Jobb kun innen godkjent milepæl; STOPP ved slutten og vent på uttrykkelig godkjenning før neste. Avslutt hver milepæl med kontrollrapport (struktur, endrede filer, build/test-resultater, CI-status lokalt vs. GitHub, commits, feil, begreper, kontrollspørsmål).
- M0 fundament (gjort) · M1 Boenheter · M2 Styresaker · M3 Oppgaver/vedlikehold · M4 Dashboard · M5 Backup/gjenoppretting og pakking for lokal kjøring. Planen kan justeres med eierens godkjenning.
