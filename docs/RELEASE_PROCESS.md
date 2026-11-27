# Utgivelsesprosess

Hvordan en utgivelse av SameieHub bygges, testes og (etter godkjenning) publiseres. Prosessen er **repeterbar**: samme kommandoer lokalt og i GitHub Actions. Ingenting her publiserer noe av seg selv: ingen Git-tag, ingen GitHub Release og ingen signering lages av skriptene eller workflowen.

## Versjon fra Git-tag

Versjonen kommer fra tagger som `v1.2.3` eller `v1.2.3-rc.1` (`scripts/Get-Version.ps1`):

| Situasjon | Versjon |
|---|---|
| På en tagget commit | `1.2.3` |
| N commits etter siste tag `v1.2.3` | `1.2.4-dev.N` |
| Ingen tag finnes | `0.0.0-dev.<antall commits>` |

Versjonen gis til byggingen (`-p:Version=`), så den står i `SameieHub.exe`, i installasjonsfilen og i «Om SameieHub». En tagg som ikke er på formen `vX.Y.Z[-forhåndsutgave]` avvises. **Taggen lages av eieren, etter godkjenning** (se under).

## Hva som bygges

`scripts/Build-Release.ps1` (PowerShell 7, Inno Setup 6 må være installert) lager i `artifacts\release\<versjon>\`:

- `SameieHub-<versjon>-win-x64-Setup.exe`, installasjonsfil (Inno Setup, per bruker, uten administrator)
- `SameieHub-<versjon>-win-x64.zip`, programmappen uten installasjon
- `SHA256SUMS.txt`, SHA-256 for begge filer
- `RELEASE-NOTES.md`, **utkast** til utgivelsesnotater fra commit-meldingene siden forrige tag, med kontrollsummene og advarselen om at filen er usignert. Skriv det om for lesere som ikke er utviklere før det publiseres.

Programmet er self-contained `win-x64` uten trimming og uten testkroker. Skriptet **stopper med feil** hvis filene inneholder databaser, logger, symbolfiler, nøkler eller hemmeligheter, hvis programmet har testkroker eller ikke er merket som det publiserte Windows-programmet, hvis versjonen i `SameieHub.exe` eller installasjonsfilen er feil, eller hvis zip-filen mangler `SameieHub.exe`.

```powershell
.\scripts\Build-Release.ps1                        # versjon fra taggene
.\scripts\Build-Release.ps1 -Version 1.0.0-rc.1    # eller gitt versjon
```

## GitHub Actions

| Workflow | Når | Hva |
|---|---|---|
| `CI` (`ci.yml`) | hver push og pull request | bygg og alle tester, røyktest av programmet, installasjon/oppdatering/avinstallasjon, avbrutt oppdatering. Installasjonsfilen beholdes 7 dager som intern artifact. |
| `Release build` (`release-build.yml`) | startes for hånd («Run workflow»), eller når en tag `v*` pushes | alt over, pluss `Build-Release.ps1`, kontroll av kontrollsummene, og at selve utgivelsesinstallasjonen består installasjonstesten. Filene beholdes 30 dager som artifact. **Lager ingen Release.** |

## Manuell godkjenning før offentlig publisering

1. Kjør `Release build` (fra `main`, for hånd) og last ned artifacten.
2. Gå gjennom [RELEASE_CHECKLIST.md](RELEASE_CHECKLIST.md). En utgivelse til **begrenset** eller **offentlig** distribusjon krever at kriteriene for den nivået er «Bestått». Ikke bruk ordet «ferdig» om noe som står som «Ikke testet».
3. Les og skriv om `RELEASE-NOTES.md`.
4. Eieren godkjenner uttrykkelig, og lager taggen (`git tag -a v1.0.0 -m "..."` og `git push origin v1.0.0`). Det starter `Release build` med den endelige versjonen.
5. Først etter at den workflowen er grønn og filene er kontrollert, lager eieren selv utgivelsen, for eksempel:

   ```powershell
   gh release create v1.0.0 --draft --title "SameieHub 1.0.0" --notes-file RELEASE-NOTES.md `
     SameieHub-1.0.0-win-x64-Setup.exe SameieHub-1.0.0-win-x64.zip SHA256SUMS.txt
   ```

   Lag den som **utkast** (`--draft`), kontroller siden, og publiser den bevisst. Filene **må** komme fra artifacten til workflowen på taggen, ikke fra en lokal bygging.

## Hvis en utgivelse viser seg å være feil

Slett ikke en publisert tag. Lag en ny, høyere versjon (`v1.0.1`) og forklar i notatene. Dataene til brukerne påvirkes ikke av en ny installasjon (se [BRUKERVEILEDNING.md](BRUKERVEILEDNING.md)).

## Gjenopprettingsprosedyrer for brukere

Brukerveiledningen (punkt 6 og 8) forklarer dem for brukeren. For utvikleren, og hva som er testet:

- **Avbrutt eller mislykket oppdatering.** Inno Setup sletter først gamle programfiler og kopierer så de nye. Feiler det midt i, er programmappen ufullstendig, men dataene er uberørt (testet i `scripts/Test-InterruptedUpdate.ps1`: etter en feil lå det bare én DLL igjen, og `SameieHub.exe` manglet). **Prosedyre:** kjør den samme installasjonsfilen på nytt. Det repareres, og programmet starter på den gamle databasen (testet).
- **Bevare databasen.** Installasjonsprogrammet rører aldri `%LOCALAPPDATA%\SameieHub`. Før en oppdatering anbefales en sikkerhetskopi.
- **Database oppgradert av en nyere versjon.** Programmet nekter å åpne en database fra en nyere versjon enn seg selv, uten å endre den. **Prosedyre:** installer den nyeste versjonen. Det finnes ingen nedgradering og ingen automatisk tilbakerulling.
- **Kopi fra før migrering.** Ved første oppstart på en eldre database lages en kopi i `pre-migration` (de tre siste). Den er en gyldig sikkerhetskopi i den gamle databaseversjonen. **Prosedyre ved behov:** start programmet, velg «Gjenopprett fra sikkerhetskopi», og velg filen. Eller, hvis programmet ikke starter: avslutt det, flytt `sameiehub.db` (og `sameiehub.db-wal`/`-shm` hvis de finnes) til en annen mappe uten å slette dem, kopier kopien til `sameiehub.db`, og start en versjon som kan åpne den. Det gjøres for hånd og av en som vet hva det betyr.
