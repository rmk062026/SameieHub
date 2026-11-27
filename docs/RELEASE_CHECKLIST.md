# Utgivelsessjekkliste (M8-6)

Status per kriterium: **Bestått**, **Ikke bestått** eller **Ikke testet**. «Bestått» betyr at det er kjørt og dokumentert (hvor står i kolonnen «Bevis»). Det som ikke er prøvd, står som «Ikke testet», uansett hvor sannsynlig det er at det virker. Sjekklisten oppdateres når noe prøves.

Sist oppdatert etter akseptansekontrollen (se [WINDOWS_ACCEPTANCE_REPORT.md](WINDOWS_ACCEPTANCE_REPORT.md)); opprinnelig M8-6 (versjon `0.8.x`, utviklingsmaskin med Windows 11 Pro, vanlig (ikke forhøyet) økt, .NET installert).

## Konklusjon

| Nivå | Vurdering | Hvorfor |
|---|---|---|
| **Klar for intern testing** | **Ja** | Alle kriterier under «Bygging og tester», «Installasjon» og «Sikkerhet» er bestått. Brukes med fiktive data. |
| **Klar for begrenset distribusjon** (noen få kjente brukere) | **Nei, ikke ennå** | Åpne akseptansekriterier: ren maskin uten .NET, en annen Windows-bruker, Firefox, at en ekte bruker har prøvd veiledningen, og en rettelse av sesjonsproblemet (anbefalt). |
| **Klar for offentlig distribusjon** | **Nei** | I tillegg: signert installasjonsfil (SmartScreen), kryptert eller på annen måte beskyttet database, kontroll av tilgjengelighet med skjermleser, bindingen mellom tilgang og åpen forbindelse (se «Ikke bestått»), og en hel runde med brukertest. |

Windows-distribusjonen er **ikke ferdig verifisert**. Den er ferdig bygget og testet så langt miljøet tillater.

## Bygging og tester

| Kriterium | Status | Bevis |
|---|---|---|
| `dotnet build` uten advarsler, alle automatiske tester | Bestått | 1733 tester, 0 advarsler (se `TESTING.md`) |
| Self-contained `win-x64`-publisering uten trimming og uten testkroker | Bestått | `Build-Release.ps1` stopper hvis testkroker finnes eller programmet ikke er merket som publisert Windows-bygg |
| Røyktest av programmet (tilgang, kode, cookie, andre start, omstart, avslutning) | Bestått | `Test-WindowsPackage.ps1`, 93 kontroller, lokalt og i CI |
| Installasjonsfil, zip, SHA-256 og utgivelsesnotater | Bestått | `Build-Release.ps1`; kontrollsummene kontrolleres i `Release build` |
| Versjonsnummer fra Git-tag, riktig i `SameieHub.exe`, installasjonsfilen og «Om SameieHub» | Bestått | `Get-Version.ps1`, `Build-Release.ps1`, `AppVersionTests` |
| Ingen database, logg, sikkerhetskopi, symbolfil eller hemmelighet i filene | Bestått | `Build-Release.ps1` (mappe og zip), `Test-WindowsPackage.ps1`, `Test-Installer.ps1` |
| GitHub Actions bygger og tester filene | Bestått | `CI` og `Release build` (se rapport for kjøringer) |
| Manuell godkjenning før offentlig publisering | Bestått (prosess) | `RELEASE_PROCESS.md`: ingen Release og ingen tag lages automatisk |

## Installasjon, bruk og oppdatering (på filsystemnivå og i testbygging)

| Kriterium | Status | Bevis |
|---|---|---|
| Installasjon per bruker uten administrator | Bestått | `Test-Installer.ps1`, 63 kontroller. Økten er vanlig, ikke forhøyet. |
| Snarvei i Start-menyen starter programmet uten argumenter | Bestått | `Test-Installer.ps1` leser snarveien, og akseptansekontrollen startet den via skallet. Et musklikk i selve Start-menyen er **ikke** prøvd. |
| Lokal tilgangsbeskyttelse, andre start åpner ny fane | Bestått | `Test-WindowsPackage.ps1`; nettleser (Chromium i Claude-appen) i M8-3 og M8-5 |
| Opprette og endre fiktive data, sikkerhetskopi | Bestått | Manuelt i nettleser, M8-5 (testbygging av samme kode) |
| Gjenoppretting med kontrollert omstart | Bestått | Manuelt i M8-5; `Test-WindowsPackage.ps1` (omstart); `WindowsStartupTests` |
| Normal avslutning | Bestått | M8-5 manuelt; `Test-WindowsPackage.ps1`; `Test-Installer.ps1` |
| Oppdatering over eksisterende installasjon, også mens programmet kjører | Bestått | `Test-Installer.ps1` |
| Avinstallasjon uten sletting av data, ny installasjon finner dataene | Bestått | `Test-Installer.ps1` |
| Avbrutt oppdatering: dataene uberørt, samme installasjonsfil reparerer | Bestått | `Test-InterruptedUpdate.ps1`, 15 kontroller |
| Database fra nyere versjon avvises uten endring | Bestått | `DatabaseUpgraderTests`, `WindowsStartupTests` |
| Kopi fra før migrering kan velges som sikkerhetskopi | Bestått | `DatabaseUpgraderTests` |
| Brukerveiledning på norsk | Bestått (skrevet) / **Ikke testet** med en ekte bruker | `BRUKERVEILEDNING.md` |
| Interaktiv installasjonsveiviser (klikke gjennom sidene), oppdatering og avinstallasjon i veiviseren | Bestått | `WINDOWS_ACCEPTANCE_REPORT.md`, punkt 1, 8 og 9 (styrt via UI Automation, ikke med mus) |
| Standardnettleseren (Edge) åpnes av snarveien, og sesjonen virker | Bestått | `WINDOWS_ACCEPTANCE_REPORT.md`, punkt 4 og 5 |

## Sikkerhet

| Kriterium | Status | Bevis |
|---|---|---|
| Tilgangsvakt for HTTP, POST og WebSocket | Bestått | `AccessGuardTests` (57), `Test-WindowsPackage.ps1` |
| Fremmed `Origin` og `Host` avvises | Bestått | `AccessGuardTests`, røyktesten |
| Test- og utviklingskroker er av i den publiserte filen | Bestått | `WindowsHostTests`, `Test-Installer.ps1`, `Build-Release.ps1` |
| Bare loopback (127.0.0.1), også med `--urls` og `Kestrel:Endpoints` | Bestått | `WindowsHostTests`, `Test-Installer.ps1` |
| Named pipe bare for samme Windows-bruker | Bestått (ACL kontrollert) / **Ikke testet** mot en annen bruker | `ActivationPipeTests` |
| Ingen hemmeligheter eller personopplysninger i logger | Bestått | `SafeLogging`-tester, røyktesten, `DatabaseUpgraderTests` |
| Ingen web-handling kan stoppe programmet uten gyldig sesjon | Bestått | `ShutdownAccessTests`, `ShutdownServiceTests` |
| Avslutning avvises etter at alle sesjoner er utløpt (rettet i M8-6) | Bestått | `ShutdownServiceTests`, `ShutdownAccessTests`, `AccessSessionTests` |
| En allerede åpen side mister tilgangen når sesjonen utløper | **Ikke bestått** (kjent, ikke løst) | Sesjonen kontrolleres bare når forbindelsen opprettes. «Avslutt» kontrollerer bare at en vilkårlig sesjon er gyldig. Utredet i `WINDOWS_ACCEPTANCE_REPORT.md`, punkt 3 (anbefaling: sesjons-id i kretsens identitet og en `CircuitHandler`). Ikke implementert. |
| Databasen er kryptert | **Ikke bestått** (ikke et krav i v1.0) | Beskrevet i brukerveiledningen |
| Installasjonsfilen er signert | **Ikke bestått** (ingen sertifikat) | Veiledningen forklarer det og kontroll av SHA-256 |

## Kompatibilitet

| Miljø | Status | Merknad |
|---|---|---|
| Windows 11 (Pro, 10.0.26300) | Bestått | Utviklingsmaskinen |
| Vanlig brukerkonto uten administratorrettigheter | Delvis | Økten er ikke forhøyet, og installasjonen krever ikke forhøyning. En konto som i tillegg er **uten** administratorgruppe er ikke prøvd. |
| Ren Windows uten .NET Runtime eller SDK | **Ikke testet** | Ingen ren maskin eller VM tilgjengelig, og det er ikke laget en uten eierens godkjenning. Programmet er self-contained, og GitHub-maskinen har .NET installert, så den dekker det ikke. **Åpent akseptansekriterium.** |
| En annen Windows-bruker (pipe, port, mapper) | **Ikke testet** | Samme grunn. |
| Edge (158) | Bestått | Standardnettleser: åpning med engangskode, navigasjon, interaktivitet, sikkerhetskopi (filen levert til nettleseren), reell gjenkobling etter 45 s pause, omstart og avslutning. Se `WINDOWS_ACCEPTANCE_REPORT.md`. |
| Firefox | **Ikke testet** | Ikke installert. |
| Chromium (innebygd nettleser i Claude-appen) | Bestått | M8-3 og M8-5 |
| Windows Terminal | Delvis | Programmet ble startet med `wt.exe` og avsluttet ordentlig. Det ble ikke kontrollert visuelt at vinduet var Windows Terminal, og Ctrl+C og lukking der er ikke prøvd. |
| SmartScreen | **Ikke testet** | Filen er usignert og har ingen omdømme. Forvent en advarsel. |
| Windows Defender | Bestått | Egen skanning av installasjonsfilen: ingen funn (signatur 1.463.493.0). Ingen andre antivirusprogrammer er prøvd. |
| Interaktiv installasjonsveiviser | Bestått | Se over |
| Skjermleser | **Ikke testet** | |
