# SameieHub: installasjon og bruk (Windows)

Denne veiledningen er for deg som skal installere og bruke SameieHub på en Windows-maskin. Du trenger ikke installere .NET eller noe annet, og du trenger ikke administratorrettigheter.

> **Status:** SameieHub er en tidlig versjon. Den er testet av utvikleren, men ikke av andre brukere eller på en ren maskin (se [RELEASE_CHECKLIST.md](RELEASE_CHECKLIST.md)). Databasen er **ikke kryptert**. Lag sikkerhetskopier, og vær forsiktig med personopplysninger (navn, telefon og e-post på eiere).

## 1. Installere

1. Last ned installasjonsfilen `SameieHub-<versjon>-win-x64-Setup.exe` **bare fra stedet du fikk utgivelsen fra** (for eksempel av den som sendte deg lenken). Ta også med filen `SHA256SUMS.txt`.
2. **Kontroller filen** før du kjører den (se 2 under). Det tar ett minutt.
3. Dobbeltklikk installasjonsfilen. Velg språk og trykk deg gjennom veiviseren. Programmet installeres for **din bruker** i `%LOCALAPPDATA%\Programs\SameieHub`. Du kan velge en snarvei på skrivebordet.
4. Du finner SameieHub i Start-menyen.

Vil du heller ikke installere noe, kan du pakke ut zip-filen (`SameieHub-<versjon>-win-x64.zip`) til en vanlig mappe og dobbeltklikke `SameieHub.exe`. Da får du ikke snarvei eller oppføring under «Apper».

## 2. Installasjonsfilen er ikke signert: hva det betyr

Installasjonsfilen er **ikke digitalt signert**, fordi et signeringssertifikat koster penger og ikke er anskaffet ennå. Det betyr at Windows ikke kan si hvem som har laget filen. Du kan få en advarsel:

- **«Windows beskyttet PC» (SmartScreen)** med teksten «Ukjent utgiver». Det er normalt for en usignert fil, men det er ikke et bevis for at filen er trygg.
- **Antivirusprogrammet** kan spørre eller advare.

**Slik gjør du det trygt:**

1. Kontroller at du fikk filen fra en du stoler på, og at du ventet den.
2. Sammenlign filens SHA-256 med den som står i `SHA256SUMS.txt` (eller i utgivelsesnotatene). I PowerShell:

   ```powershell
   Get-FileHash .\SameieHub-<versjon>-win-x64-Setup.exe -Algorithm SHA256
   ```

   Tallkoden (hash) må være **identisk** med den i `SHA256SUMS.txt`. En kontrollsum bekrefter bare at filen er den samme som ble bygget. Den sier ikke at utvikleren er til å stole på, og den hjelper ikke hvis kontrollsummen kom fra samme upålitelige sted som filen.
3. **Er hashen ulik, eller er du i tvil: ikke kjør filen.** Slett den, og spør den som ga deg den.
4. Bare hvis du har gjort punkt 1 og 2, kan du velge «Mer info» og «Kjør likevel» i SmartScreen. Ikke slå av SmartScreen eller antivirus, og ikke gjør unntak for hele mappen.

Når filen er signert i en senere versjon, forsvinner denne advarselen.

## 3. Starte og avslutte

- **Start:** velg SameieHub i Start-menyen. Det kommer opp et svart vindu som heter «SameieHub» (selve programmet), og nettleseren åpnes av seg selv. Du trenger ikke logge inn: programmet gir nettleseren en engangskode, og bare den nettleseren kan bruke programmet. Adressen du ser i nettleseren fungerer ikke alene hvis du kopierer den til en annen nettleser.
- **Åpne igjen:** lukket du nettleseren, men programmet kjører? Velg SameieHub i Start-menyen en gang til. Da åpnes en ny fane i programmet som allerede kjører.
- **Avslutte:** velg **«Avslutt SameieHub»** nederst i menyen i nettleseren, og bekreft. Du kan også lukke det svarte vinduet. Å lukke bare nettleseren avslutter ikke programmet.
- **Versjonsnummer:** velg **«Om SameieHub»** nederst i menyen. Versjonen står også under Innstillinger > Apper > Installerte apper > SameieHub.

## 4. Hvor dataene lagres

Alt lagres lokalt på maskinen din, ingenting sendes til internett. Mappen er `%LOCALAPPDATA%\SameieHub` (skriv det i adresselinjen i Filutforsker):

| Fil eller mappe | Innhold |
|---|---|
| `sameiehub.db` | Selve databasen med alle dataene dine |
| `logs\` | Loggfiler (uten personopplysninger) som hjelper ved feil |
| `pre-migration\` | En kopi av databasen fra rett før en oppgradering av databasen (de tre siste) |
| `restore\` | Dataene du hadde før en gjenoppretting |

Installasjon, oppdatering og avinstallasjon **endrer ikke** denne mappen.

## 5. Sikkerhetskopi og gjenoppretting

- **Lage sikkerhetskopi:** gå til **Sikkerhetskopi** i menyen og velg «Lag og last ned sikkerhetskopi». Du får én fil (`.db`) i nedlastingsmappen. Flytt den til et trygt sted, gjerne på en annen disk. Filen er ikke kryptert og inneholder personopplysninger: ikke send den i vanlig e-post eller legg den i en delt mappe. Lag en kopi jevnlig, og alltid før du gjenoppretter eller oppdaterer.
- **Gjenopprette:** under **Sikkerhetskopi**, åpne «Gjenopprett fra sikkerhetskopi», velg filen, les sammenligningen mellom dagens data og kopien, og bekreft. Gjenopprettingen er da **klargjort, men ikke gjennomført**. Velg **«Avslutt og start på nytt»**: programmet lukkes og starter igjen av seg selv, og gjenopprettingen gjennomføres under oppstarten. Du ser resultatet på forsiden. Starter ikke programmet igjen, starter du det fra Start-menyen. Dataene du hadde før blir liggende i mappen `restore`. Du kan avbryte frem til omstarten.

## 6. Oppdatere

1. Lag en sikkerhetskopi (punkt 5).
2. Kjør den nye installasjonsfilen (etter at du har kontrollert den, punkt 2). Kjører SameieHub, spør installasjonen om den skal be programmet avslutte på en trygg måte. Svar ja. Programmet avsluttes aldri med makt.
3. Start SameieHub. Er databasen fra en eldre versjon, oppgraderes den ved første oppstart, og det legges en kopi i `pre-migration`.

Dataene røres ikke av selve installasjonen. Programmet har ingen automatisk oppdatering.

**Gikk oppdateringen i stykker** (strømbrudd, avbrutt, en feilmelding)? Programmappen kan da være ufullstendig, men **dataene dine er uberørt**. Kjør **samme installasjonsfil en gang til**. Det reparerer programmet.

## 7. Avinstallere og data

- **Avinstallere:** Innstillinger > Apper > Installerte apper > SameieHub > Avinstaller. Du får beskjed om hvor dataene ligger. **Dataene slettes ikke.** Installerer du SameieHub på nytt, finner det dataene igjen.
- **Beholde dataene:** gjør ingenting. Vil du flytte dem til en annen maskin, bruk en sikkerhetskopi (punkt 5).
- **Fjerne dataene helt:** lag først en sikkerhetskopi hvis du kan trenge dem. Avslutt SameieHub, og slett mappen `%LOCALAPPDATA%\SameieHub`. Det kan ikke angres.

## 8. Hvis noe går galt

- **Programmet starter ikke:** det vises en melding som forklarer hva som skjedde. Typiske meldinger:
  - «SameieHub kjører allerede»: se etter vinduet «SameieHub» i oppgavelinjen.
  - «Databasen er laget av en nyere versjon»: du har startet en eldre versjon enn den som sist brukte databasen. Installer den nyeste versjonen. Ikke rediger eller slett databasefilen.
  - «En gjenoppretting ble avbrutt og ikke kunne fullføres trygt»: ingenting er slettet. Se «Hvis gjenopprettingen står uavklart» i [ARCHITECTURE.md](ARCHITECTURE.md), og få hjelp.
  - «Databasen kunne ikke oppgraderes»: meldingen sier hvor kopien fra før oppgraderingen ligger (`pre-migration`). Ikke slett den.
- **Nettleseren viser «Ingen tilgang»:** adressen virker bare i nettleseren programmet selv åpnet. Velg SameieHub i Start-menyen for å åpne en ny fane.
- **Hente fram en eldre tilstand:** bruk en sikkerhetskopi (punkt 5). Filene i `pre-migration` er også gyldige sikkerhetskopier fra før en oppgradering: du kan velge en av dem under «Gjenopprett fra sikkerhetskopi». SameieHub ruller aldri tilbake databasen automatisk.
- **Få hjelp:** ha klart versjonsnummeret (punkt 3), meldingen du fikk, og de siste loggfilene i `%LOCALAPPDATA%\SameieHub\logs`. Loggene inneholder ikke personopplysninger, men se gjennom dem før du sender dem. Send ikke databasen eller sikkerhetskopier. Kontaktinformasjon og dokumentasjon: [github.com/rmk062026/SameieHub](https://github.com/rmk062026/SameieHub).
