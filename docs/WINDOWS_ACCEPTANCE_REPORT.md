# Akseptanserapport: Windows-distribusjonen etter M8

Dette er en avgrenset akseptansekontroll, ikke en utviklingsmilepæl. Det er ikke endret noe i programmet, databasen eller domenet. Kontrollen ble gjort på commit `1f088f9` (versjon `0.9.0-acc.1` og `0.9.0-acc.2`, bygget lokalt fra den commiten).

**Status:** Windows-distribusjonen er **klar for intern testing, men ikke ferdig verifisert.** Den er **ikke** klar for begrenset eller offentlig distribusjon. Se [RELEASE_CHECKLIST.md](RELEASE_CHECKLIST.md).

## 1. Metode og hva «testet» betyr her

Hvert punkt har en **type**:

- **A = automatisert test** (xUnit, bUnit eller PowerShell-skript som kjører uten at et menneske klikker).
- **M = manuell test**: jeg utførte handlingen i et ekte program eller en ekte nettleser og leste resultatet. Her ble veiviseren og dialogene styrt gjennom Windows' grensesnitt-automatisering (UI Automation), ikke med en mus.
- **K = kodegjennomgang**: vurdert ut fra koden. Et punkt som bare er K, står **aldri** som «Bestått».

Miljø: Windows 11 Pro (10.0.26300), vanlig bruker i en **ikke forhøyet** økt, .NET er installert (runtime 8 og 10), Windows Defender, Microsoft Edge 158 (standardnettleser), Brave, Chromium i Claude-appen. Firefox er ikke installert. Alle prøver brukte en egen midlertidig database (`Database__Path`) i en midlertidig mappe. Den ordinære databasen og demoen ble ikke brukt. Den virkelige datamappen (`%LOCALAPPDATA%\SameieHub`) ble sammenlignet filfor filen før og etter avinstallasjonen og var uendret.

**Merk om koden og nettleser-flyten.** Den installerte filen har ingen testkroker, så jeg kan ikke hente engangskoden fra den. Den delen av nettleserflyten som krever å styre siden (opprette data, sikkerhetskopi, omstart) ble derfor gjort mot **testbyggingen** (`-TestHooks`) av samme kode, i en egen Edge-instans med egen profil. Det som kunne observeres uten kroker på den installerte filen, ble observert der (se punkt 2.2).

## 2. Kontrollpunkter

### 2.1 Interaktiv installasjon, oppdatering og avinstallasjon

| # | Kontrollpunkt | Type | Status | Utførte handlinger og observasjoner |
|---|---|---|---|---|
| 1 | Interaktiv Inno Setup-veiviser | M | **Bestått** | `SameieHub-0.9.0-acc.1-win-x64-Setup.exe` ble startet uten `/SILENT`, med `/DIR=` til en midlertidig mappe. Veiviseren (norsk) viste sidene «Velg mappen …», «Velg tilleggsoppgaver» (skrivebordssnarvei, ikke valgt), «Klar til å installere», fremdrift og «Fullfører installasjonsprogrammet». Knappene Neste, Installer og Ferdig ble trykket via UI Automation. Veiviseren avsluttet, og programmet var installert. |
| 2 | Installasjon uten forhøyning | M | **Bestått** | Ingen UAC-spørsmål dukket opp, og installasjonen gikk gjennom fra en ikke forhøyet økt (en UAC-dialog ville ha stoppet automatiseringen, siden den ligger på et eget skrivebord). Programmet ligger under brukerprofilen, og oppføringen er i `HKCU`. Det ble ikke prøvd med en konto som mangler administratorgruppen helt (se 2.5). |
| 3 | Snarvei i Start-menyen | M | **Bestått** (med forbehold) | Snarveien `SameieHub.lnk` finnes i brukerens Start-meny-mappe, peker på `SameieHub.exe` uten argumenter. Jeg **startet snarveien via Windows-skallet**, ikke ved å klikke i selve Start-menyen med musen. |
| 4 | Standardnettleseren åpnes | M | **Bestått** | Da snarveien ble startet, åpnet **Microsoft Edge** (standardnettleseren) seg av seg selv. Edge-vinduets tittel ble «Oversikt – SameieHub». Edge hadde flere etablerte forbindelser til programmets port. |
| 5 | Engangskode og sesjon fungerer | M + A | **Bestått** | At siden er rendret med tittelen «Oversikt – SameieHub» i Edge viser at koden ble løst inn og cookien godtatt (uten gyldig cookie er svaret «Ingen tilgang»). Selve engangskoden, enkel bruk, cookie-egenskaper og avvisning er automatisert i `Test-WindowsPackage.ps1` (93 kontroller) og `AccessGuardTests`. |
| 6 | Avslutt fra grensesnittet | M | **Bestått** (testbygging) | Se 2.3. |
| 7 | Start igjen fra Start-menyen, dataene bevart | M | **Bestått** | En fiktiv boenhet ble lagt i den isolerte databasen mens programmet var stoppet. Snarveien ble startet igjen, programmet kjørte og Edge åpnet. Raden fantes fortsatt etter oppdateringen (7+8). |
| 8 | Oppdatering over eksisterende installasjon, mens programmet kjører | M | **Bestått** | `0.9.0-acc.2` ble kjørt i veiviseren mens `acc.1` kjørte. Veiviseren stilte spørsmålet «SameieHub kjører … Skal installasjonen be SameieHub avslutte på en trygg måte nå?». «Ja» ble trykket. Programmet avsluttet (loggen viser «asked to stop», ikke drept), filene ble byttet, oppføringen viste `0.9.0-acc.2`, og den fiktive raden i databasen var der. I Edge sto meldingen «SameieHub avsluttes». |
| 9 | Avinstallasjon, uten sletting av data | M | **Bestått** | Avinstallasjonsprogrammet ble kjørt interaktivt mens programmet kjørte. Spørsmål om å avslutte (Ja), «Er du sikker …» (Ja), «Avinstallasjonen var vellykket» (OK) og meldingen om at dataene er beholdt i `%LOCALAPPDATA%\SameieHub` (OK). Programfil, snarvei og oppføring var borte, programmet var stoppet, databasen og loggene i testmappen var der, og den virkelige datamappen var uendret. |
| 10 | Ny installasjon finner dataene | A | **Bestått** (automatisert) | Dekket av `Test-Installer.ps1` (63 kontroller). Ikke gjentatt manuelt i denne runden. |
| 11 | Avbrutt oppdatering | A | **Bestått** | `Test-InterruptedUpdate.ps1` (M8-6). Ikke gjentatt. |

### 2.2 Bruk i nettleser (Edge, egen profil, testbygging)

Kjørt av et midlertidig skript som styrer Edge gjennom Chrome DevTools Protocol (Edge 158, uten vindu, egen profil som slettes etterpå). Hver linje under er en kontroll med resultat.

| # | Kontrollpunkt | Type | Status | Observasjon |
|---|---|---|---|---|
| 12 | Åpning med engangskode | M | **Bestått** | Edge ble sendt til startadressen. Siden ble «Oversikt – SameieHub». `document.cookie` er tom (cookien er HttpOnly). Adressen ble `/` uten koden. |
| 13 | Navigasjon: Oversikt, Styresaker, Oppgaver, Boenheter, Elbillading, Om, Sikkerhetskopi | M | **Bestått** | Sidetittelen skiftet riktig for alle syv rutene (klient-navigasjon). Menyen hadde de seks punktene i riktig rekkefølge. «Om SameieHub» viste versjonen `0.9.0-acc.2`. Det er ikke lagt inn data på Elbillading, så bare sidene åpnes. |
| 14 | Blazor-interaktivitet | M | **Bestått** | Adressefeltet ble fylt, «Lagre» trykket, og boenheten «Testveien 1» ble lagret og vist (en fiktiv adresse). Spørsmålet i «Avslutt SameieHub» åpnes og lukkes. |
| 15 | Last ned sikkerhetskopi | M | **Bestått** (delvis) | «Lag og last ned» ga en bekreftelse i siden og leverte en fil på 90 112 byte til nettleseren. At filen ble lagret på disk i nedlastingsmappen er **ikke** kontrollert (Edge uten vindu). Gjenoppretting av en slik fil ble gjort i M8-5. |
| 16 | Tilkobling etter midlertidig brudd | M | **Bestått** | Prosessen ble satt på pause i 45 sekunder. Edge viste dialogen «Kobler til serveren på nytt …». Da programmet fortsatte, var dialogen borte, siden var uendret, og siden reagerte (spørsmålet om å avslutte åpnet). (En første test med «offline» i nettleseren er **ikke regnet**, fordi den ikke påvirket forbindelsen.) |
| 17 | Avslutning og omstart | M | **Bestått** | Omstart (`--restart`, testkommando): den åpne Edge-siden viste «SameieHub starter på nytt» uten gjenkoblingsdialog, et nytt program startet (annet prosessnummer), Edge logget inn med den nye koden, og boenheten fantes. Avslutning fra grensesnittet: «SameieHub avsluttes», programmet var borte, sperren fri, ingen gjenkoblingsdialog. Omstart via knappen «Avslutt og start på nytt» med en gjenoppretting ble gjort i M8-5 (Chromium). |
| 18 | Edge som standardnettleser på den installerte filen | M | **Bestått** | Se 4. Tittelen gikk fra «Oversikt – SameieHub» til «SameieHub avsluttes» da programmet ble stoppet. |
| 19 | **Firefox** | – | **Ikke testet** | Ikke installert, og jeg installerer ikke programvare uten godkjenning. |
| 20 | Chromium (Claude-appen) | M | **Bestått** | Kjørt i M8-3 og M8-5. Ikke gjentatt. |

### 2.3 Øvrig fra oppdraget

| # | Kontrollpunkt | Status | Merknad |
|---|---|---|---|
| 21 | Oversikt, Styresaker, Oppgaver, Boenheter, Elbillading brukes | **Bestått** for åpning og navigasjon; **Ikke testet** for å registrere og endre data på hver side | Bare boenheter ble opprettet. De andre sidene er dekket av 1733 automatiske tester, ikke av denne manuelle runden. |
| 22 | Windows Terminal | **Ikke testet** (gjentatt) | Se `RELEASE_CHECKLIST.md`. |
| 23 | SmartScreen | **Ikke testet** | Filen ble laget lokalt, så den har ingen «Mark of the Web» og utløser ikke SmartScreen. Dette sier ingenting om hva en nedlastet fil gjør. |
| 24 | Annen Windows-bruker, ren maskin | **Ikke testet** | Se punkt 5. |

### 2.4 Nye feil og observasjoner

- **Ingen nye feil i programmet funnet.**
- En første kjøring av nettleserskriptet mislyktes helt, fordi skriptet koblet til en gammel Edge-fane. Det var en feil i **testskriptet**, ikke i programmet. Alt ble kjørt på nytt fra rent utgangspunkt, og regnes bare fra den kjøringen.
- **Observasjon (ikke en feil):** meldingen etter avinstallasjon peker alltid på standard datamappe (`%LOCALAPPDATA%\SameieHub`), også når en test brukte en annen databasebane. For vanlige brukere er det riktig. Det er ikke endret.
- **Observasjon:** under prøven med standardnettleseren ble det liggende to faner i din Edge-økt («Oversikt – SameieHub» og «SameieHub avsluttes», nå døde adresser til et program som er stoppet). Lukk dem. Jeg styrte ikke din Edge-profil, bare observerte vindustittelen.

## 3. Sikkerhet: sesjonen til en etablert Blazor-forbindelse

**Kjent problem, ikke løst.** `AccessGuardMiddleware` kontrollerer cookien når en forbindelse opprettes (negotiate og WebSocket), men ikke for hver melding etterpå. En Blazor-forbindelse (krets) som ble åpnet med en gyldig sesjon, kan derfor fortsette å lese og endre data etter at sesjonen er utløpt, så lenge forbindelsen holder. Å åpne en ny forbindelse krever ny gyldig cookie.

**Presisering av M8-6.** Kontrollen som ble lagt inn der (`AccessSession.HasValidSession()`, kalt av `ShutdownService.Request`) spør om **en vilkårlig** sesjon fortsatt er gyldig, ikke om **forbindelsens egen** sesjon er det. Den er derfor bare en delvis begrensning av «avslutt og start på nytt», og **problemet er ikke løst**. Det står som «Ikke bestått» i sjekklisten.

**Undersøkelse: hva finnes i ASP.NET Core og Blazor Server** (typene er kontrollert som tilstedeværende i .NET 10.0.15 på maskinen; den foreslåtte løsningen er **ikke** prøvd):

1. **`HttpContext.User` ved opprettelse av kretsen.** Blazor Server kopierer `ClaimsPrincipal` fra forespørselen som åpner forbindelsen inn i `AuthenticationStateProvider` for den kretsen. Vakten kan sette en identitet med en krav-verdi «sesjons-id» (en hash av sesjonen, aldri selve cookien) på `HttpContext.User` for forespørslene til `/_blazor`.
2. **`CircuitHandler.CreateInboundActivityHandler`.** Kalles for hver innkommende melding til kretsen, og kan avslå å sende den videre. Der kan en sjekk «gjelder sesjonen med denne id-en fortsatt?» gjøres, og ved nei avvises meldingen og siden sendes til en fullstendig omlasting (som gir «Ingen tilgang»).
3. **`RevalidatingServerAuthenticationStateProvider`.** Sjekker på et intervall at påloggingen fortsatt gjelder, og gjør brukeren anonym hvis ikke. Det hjelper bare komponenter som bruker autorisasjon (`AuthorizeRouteView`, `[Authorize]`). Det stopper ikke hendelser i komponenter som ikke bruker den. Passer som tillegg, ikke som eneste vern.
4. **Ingen offentlig metode for å lukke en krets fra serveren.** Vanlige måter er å la en handler avvise meldinger, å tvinge en omlasting fra klienten, eller å kaste et unntak (grov, og viser en feilboks).

**Anbefaling.** Kombiner 1 og 2, og la 3 være valgfritt:

- `AccessSession` får `IsValidSessionId(string)` (sesjonene er allerede lagret som hasher, så id-en kan være hashen i seg selv eller en egen tilfeldig id per sesjon).
- Vakten legger id-en på `HttpContext.User` for `/_blazor`-forespørsler (etter at cookien er validert).
- En ny `CircuitHandler` leser id-en fra kretsens autentiseringstilstand ved åpning, og i `CreateInboundActivityHandler` avviser alle meldinger etter utløp og sender siden til omlasting.

**Kompleksitet:** middels. Omtrent 100 linjer i to nye klasser, litt i vakten og i `AccessSession`, og én registrering. Ikke en arkitekturendring.

**Nødvendige tester:**

- Enhetstester for sesjons-id-kontrollen og handleren med fiktiv tid (utløp, gyldig, ukjent id, flere sesjoner der bare én utløper).
- En integrasjonstest som åpner en krets og ser at meldinger avvises etter utløp. Den trenger en SignalR-klient, som **ikke** finnes i testprosjektet i dag. Pakken `Microsoft.AspNetCore.SignalR.Client` må da godkjennes. Ellers må den delen testes manuelt.
- En manuell nettlesertest (Edge og Chromium) med en testbygging der sesjonslevetiden kan settes kort (en testkrok, som ikke finnes i den installerte filen), som kontrollerer at siden sendes til «Ingen tilgang» etter utløp og at to faner oppfører seg riktig.
- Eksisterende tester for vakten og røyktesten må fortsatt bestå.

**Risiko for å bryte Blazor:** lav til middels.

- *Gjenkobling:* en gjenkobling til samme krets bruker kretsens opprinnelige identitet, så en gyldig sesjon påvirkes ikke. En utløpt sesjon vil bli avvist, som ønsket.
- *Rekkefølge:* meldinger som `OnRenderCompleted` og JS-kall må også gå gjennom handleren. Avvises alt etter utløp, er det riktig, men det må testes at ingenting henger.
- *Flere faner:* hver fane er en egen krets med samme cookie. Utløp er felles, så alle faner avvises samtidig.
- *Navigasjon:* å tvinge omlasting fra en handler må gjøres på en måte som ikke kaster før kretsen er ferdig initialisert (testes).
- *Ytelse:* en sjekk per melding mot en liten liste i minnet er ubetydelig.

**Gjenstående til den er rettet:** programmet er et enbrukerprogram på én maskin. En utløpt sesjon etterlater en åpen fane som fortsatt virker til den lukkes. Det er dokumentert i brukerveiledningen og sjekklisten. Rettingen anbefales før **begrenset** distribusjon, men krever godkjenning.

## 4. Standardnettleseren og den installerte filen (hva som kunne observeres)

Siden den installerte filen ikke har testkroker, er dette det eneste som kunne observeres uten å styre din Edge:

- Edge åpnet seg selv ved start (punkt 4), og vinduets tittel ble «Oversikt – SameieHub» (rendret av Blazor: kretsen fungerte).
- Ved stans (`SameieHub.exe --stop`, og ved oppdateringen) ble tittelen «SameieHub avsluttes».
- Programmet lyttet bare på 127.0.0.1 (kontrollert i `Test-Installer.ps1`).

## 5. Ren Windows-maskin: hva som trengs og testoppskrift

**Hva som mangler for å teste.** Alle tester så langt kjørte på en maskin som har .NET installert, og GitHub-maskinen har det også. Programmet er self-contained, så det skal ikke trenge .NET, men det er **ikke vist** at det virker uten. En ren maskin kan også vise andre ting: manglende Visual C++-komponenter (usannsynlig, men ikke prøvd), SmartScreen på en fil med Mark of the Web, og standardnettleseren til en ny bruker.

**Hva maskinen din har** (lest, ikke endret): Windows 11 Pro, 16 GB RAM, 63 GB ledig, virtualisering er på (`HypervisorPresent = True`). **Windows Sandbox er ikke aktivert** (`WindowsSandbox.exe` finnes ikke). VirtualBox er ikke installert. Å aktivere Sandbox krever administratorrettigheter og omstart, og er derfor ikke gjort.

**Anbefalt løsning: Windows Sandbox.** Den starter en helt ren, midlertidig Windows uten .NET, og alt slettes når vinduet lukkes. Ingenting på din egen maskin endres, utenom at funksjonen aktiveres én gang.

### Oppskrift (krever din godkjenning for trinn 1)

1. **Aktiver Windows Sandbox (én gang, som administrator):** `Slå Windows-funksjoner av eller på` > kryss av «Windows Sandbox» > OK > start maskinen på nytt. Eller i en PowerShell som administrator: `Enable-WindowsOptionalFeature -Online -FeatureName Containers-DisposableClientVM -All`.
2. **Lag en mappe med testfilene** på din egen maskin, for eksempel `C:\SameieHubTest`, med bare: `SameieHub-<versjon>-win-x64-Setup.exe`, `SHA256SUMS.txt` og denne oppskriften. Hent filene fra artifacten til `Release build`-arbeidsflyten (ikke fra en lokal bygging).
3. **Lag en konfigurasjonsfil** `SameieHub.wsb` med skrivebeskyttet deling av mappen:

   ```xml
   <Configuration>
     <MappedFolders>
       <MappedFolder>
         <HostFolder>C:\SameieHubTest</HostFolder>
         <SandboxFolder>C:\Users\WDAGUtilityAccount\Desktop\Test</SandboxFolder>
         <ReadOnly>true</ReadOnly>
       </MappedFolder>
     </MappedFolders>
     <Networking>Enable</Networking>
   </Configuration>
   ```

4. **Dobbeltklikk `SameieHub.wsb`.** Du får en ren Windows. Kontroller at .NET mangler: åpne PowerShell og kjør `dotnet --list-runtimes` (skal feile med «not recognized») og `Test-Path "C:\Program Files\dotnet"` (skal gi `False`).
5. **Kontroller filen:** `Get-FileHash .\Test\SameieHub-<versjon>-win-x64-Setup.exe` og sammenlign med `SHA256SUMS.txt`. Kopier installasjonsfilen til skrivebordet i Sandbox (så den får en lokal kopi, som en nedlastet fil).
6. **Installer interaktivt** (dobbeltklikk). Noter om SmartScreen eller en annen advarsel vises, og hva den sier. Fullfør veiviseren. Noter om noe ber om administratorrettigheter (skal ikke).
7. **Start SameieHub fra Start-menyen.** Kontroller at nettleseren (Edge) åpnes, at siden vises uten innlogging, og at versjonen under «Om SameieHub» stemmer.
8. **Bruk programmet** med fiktive data: opprett en boenhet, en styresak, en oppgave, en lader og en avlesning. Lag en sikkerhetskopi (filen havner i nedlastinger). Gjenopprett den: «Avslutt og start på nytt», og kontroller resultatet på forsiden.
9. **Avslutt** med «Avslutt SameieHub», og start på nytt fra Start-menyen. Kontroller at dataene er der.
10. **Åpne igjen** fra Start-menyen mens programmet kjører: en ny fane skal åpnes.
11. **Oppdater:** kopier en nyere installasjonsfil inn og kjør den over (mens programmet kjører). Kontroller at dataene er bevart.
12. **Avinstaller** fra Innstillinger > Apper. Kontroller at `%LOCALAPPDATA%\SameieHub` er beholdt, og at en ny installasjon finner dataene.
13. **Firefox og Edge:** installer Firefox i Sandbox (internett er på) og gjenta trinn 7 til 9 med Firefox som standardnettleser. Gjenta med Edge.
14. **Noter alt** i `RELEASE_CHECKLIST.md`: hver rad som er prøvd, med dato, versjon og hva du så. Lukk Sandbox-vinduet: alt slettes.

**Alternativ (hvis Sandbox ikke er mulig):** en virtuell maskin (Hyper-V «Hurtigopprettelse» med Windows 11-utviklingsmaskinen, eller VirtualBox) fra et rent Windows 11-bilde. Gjør de samme trinnene fra 4. Opprett en vanlig lokal bruker uten administratorrettigheter i VM-en, og gjør trinnene i den, for å dekke «vanlig brukerkonto». Test også «en annen Windows-bruker» ved å logge inn som en annen vanlig bruker mens første brukers program kjører (kan SameieHub på port og pipe nås derfra? Forventet svar er nei).

## 6. Hva du selv må teste

1. **Ren maskin** etter oppskriften over (det viktigste, og det som krever din godkjenning for å slå på Windows Sandbox).
2. **Firefox** (og gjerne Brave, som du allerede har).
3. **En annen Windows-bruker:** at den ikke kan koble til programmet eller pipen (forventet: nei).
4. **Klikk i selve Start-menyen** (jeg startet snarveien via skallet).
5. **Brukeren som ikke er utvikler:** les `BRUKERVEILEDNING.md` og installer etter den. Skriv ned hva som er uklart.
6. **Opprett data på alle sidene** (Styresaker, Oppgaver, Elbillading) i den installerte filen, ikke bare på boenheter.
7. **Lukk de to døde fanene** i Edge fra min test (se 2.4).

## 7. Oppdatert distribusjonsstatus

| Nivå | Vurdering |
|---|---|
| Klar for intern testing | **Ja** (som før) |
| Klar for begrenset distribusjon | **Nei, ikke ennå.** Gjenstår: ren maskin, annen Windows-bruker, Firefox, test av en ekte bruker, og anbefalt rettelse av sesjonsproblemet. |
| Klar for offentlig distribusjon | **Nei** |

Nye bevis i denne runden: interaktiv veiviser, installasjon uten forhøyning, standardnettleser (Edge), Edge-flyt, reell gjenkobling og interaktiv oppdatering og avinstallasjon. Ingenting ble funnet som endrer status oppover eller nedover utover det.
