# UI-design

## Prinsipper
- Norsk grensesnitt, enkelt og rolig: lys bakgrunn, mørk sidemeny, én aksentfarge, lite animasjon.
- Bootstrap 5 (følger malen) + egen CSS. Ingen UI-bibliotek.
- Konsekvent mønster per område: **liste → registrer → rediger**, med tydelig vei tilbake.
- Tilgjengelighet: synlig fokusmarkering, tastaturnavigasjon, `label` på alle felt, `role="alert"`/`role="status"` på meldinger, tabell med `caption` og `scope`.

## Struktur
```
Components/
  Layout/    MainLayout (rammen), NavMenu (sidemeny/mobilmeny)
  Shared/    PageHeader (tittel, beskrivelse, handlinger), DetailItem (felt i detaljvisning), ComingSoon (plassholder)
  Pages/
    Dashboard.razor, Tasks.razor   (plassholdere)
    HousingUnits/  HousingUnitList, HousingUnitDetail, HousingUnitCreate, HousingUnitEdit,
                   HousingUnitForm, HousingUnitFormModel, HousingUnitFieldMessages,
                   HousingUnitStatusBadge, HousingTypeText
    BoardCases/    BoardCaseList, BoardCaseDetail, BoardCaseCreate, BoardCaseEdit,
                   BoardCaseForm, BoardCaseFormModel, BoardCaseFieldMessages,
                   BoardCaseStatusBadge, BoardCasePriorityBadge, BoardCaseText
    Charging/      ChargingIndex, ChargerCreate/Edit/Detail/Form(+Model, FieldMessages), ReadingSetCreate,
                   ReconciliationPage/View, MeterHistoryPage, MeterReadingsPanel, ReadingTimeField,
                   UnitChargingSection, DashboardChargingSection, ChargingText/Input/ErrorText, ChargerStatusBadge
wwwroot/app.css  designvariabler og felles stiler
```

## Navigasjon og ruter
| Rute | Side | Status |
|---|---|---|
| `/` | Oversikt | Funksjonell (R1) |
| `/boenheter` | Oversikt | Funksjonell |
| `/boenheter/ny` | Registrer | Funksjonell |
| `/boenheter/{id}` | Detaljvisning | Funksjonell |
| `/boenheter/{id}/rediger` | Rediger alle opplysninger | Funksjonell |
| `/styresaker` | Oversikt | Funksjonell |
| `/styresaker/ny` | Registrer | Funksjonell |
| `/styresaker/{id}` | Detaljvisning med status og vedtak | Funksjonell |
| `/styresaker/{id}/rediger` | Rediger | Funksjonell |
| `/oppgaver` | Oversikt | Funksjonell |
| `/oppgaver/ny` | Registrer (`?boenhet=` og `?styresak=` forhåndsvelger) | Funksjonell |
| `/oppgaver/{id}` | Detaljvisning med hovedhandling, status og sletting | Funksjonell (R2) |
| `/oppgaver/{id}/rediger` | Rediger | Funksjonell |
| `/elbillading` | Oversikt (oppsett av anlegg, tall, hovedmåler, siste avstemming, ladere) | Funksjonell |
| `/elbillading/ny` | Registrer lader (`?boenhet=` forhåndsvelger) | Funksjonell |
| `/elbillading/{id}` | Lader med undermåler og avlesninger | Funksjonell |
| `/elbillading/{id}/rediger` | Rediger lader og sikringsskap | Funksjonell |
| `/elbillading/avlesning` | Samlet avlesning: hovedmåler og undermålere | Funksjonell |
| `/elbillading/avstemming` | Avstemming for valgt periode | Funksjonell |
| `/elbillading/maler/{id}` | Historikk for en måler | Funksjonell |
| `/innstillinger/sikkerhetskopi` | Lag og last ned sikkerhetskopi; kontroller, bekreft og forbered gjenoppretting; resultat etter omstart | Funksjonell |

Sidemeny på bred skjerm (≥ 768 px); på smal skjerm en toppstripe med menyknapp.

## Stil
- Farger og radius er CSS-variabler i `:root` i `app.css` (`--sh-primary`, `--sh-sidebar`, …). Endre der for å endre hele designet.
- Layout-stiler ligger i `*.razor.css` (Blazor CSS-isolasjon); felles stiler (knapper, kort, tabell, skjema) i `app.css`.
- Ikoner er små inline SVG-er i `NavMenu.razor`.
- Statusmerkelapper (`.status-badge`) har alltid tekst; fargen er bare en hjelp. Styresaker: Ny (blå), Under behandling (gul), Vedtatt (grønn), Avsluttet (grå); prioritet: Lav (grå), Normal (blå), Høy (rød).

## Boenheter
- **Oversikt:** tre tall (registrerte, aktive, inaktive), søkefelt, filter Aktive/Inaktive/Alle (standard: Aktive) og tabell med adresse (og bolignummer), eier (begge navn, «Navn 1 og Navn 2»), seksjon, status og handlingene Åpne/Rediger. Sortert på adresse med tall i naturlig rekkefølge («2A» før «10B»). På smal skjerm skjules eier- og seksjonskolonnen, og eiernavn vises under adressen. Egne tomtilstander for tom database og for ingen treff («Nullstill søk og filter»), pluss lastetilstand og feilmelding med «Prøv igjen».
- **Detaljvisning:** kort for boliginformasjon, eier og kontakt, og merknader. Manglende valgfrie felt vises som «Ikke registrert». Eier 2 vises bare når den er registrert (da merkes eierne «Eier 1»/«Eier 2»). Telefon og e-post er lenker. Knappene Rediger og Deaktiver/Reaktiver ligger i toppen, med tilbakemelding etter statusendring.
- **Skjema (registrer og rediger):** felles `HousingUnitForm` med tre grupper: Boliginformasjon, Eier og kontaktinformasjon, Andre opplysninger. Passende inputtyper (tall, `tel`, `email`, nedtrekksliste, flerlinjet tekst). Eier 2 skjules bak «+ Legg til eier 2» og kan fjernes med «Fjern eier 2» (fjerner bare eier 2). Feil vises ved feltet på norsk, verdiene beholdes, og knappen låses mens lagring pågår.
- **Etter lagring:** brukeren sendes til detaljsiden med melding (`?status=opprettet` / `?status=endret`).
- **Status:** «Aktiv»/«Inaktiv» vises som tekst i en farget merkelapp (fargen er bare en hjelp).
- **Rendermodus:** Interactive Server uten prerendering, slik at data hentes én gang per side.

## Sletting av boenhet (M1F)
- Nederst på detaljsiden ligger et eget kort, «Slett boenhet», med en rød (farevarslende) knapp. Knappen sletter ikke selv.
- Et klikk åpner en bekreftelse (`role="alertdialog"`) med adressen i overskriften, advarselen «Dette kan ikke angres», og at eier- og kontaktinformasjon også slettes. Valgene er «Ja, slett permanent» og «Avbryt». Avbryt lukker bekreftelsen uten å slette noe. Knappene låses mens sletting pågår.
- Ved suksess går brukeren til oversikten med meldingen «Boenheten er slettet permanent.» (`?status=slettet`).
- Feil vises i kortet: «knyttet til styresaker eller oppgaver» (blokkert, med forslag om å fjerne koblingene eller deaktivere), «finnes ikke lenger» og en generell databasefeil.
- Deaktiver/Reaktiver finnes fortsatt øverst og er den anbefalte måten å skjule en boenhet på.

## Styresaker (M2D)
- **Liste (R3):** hurtigvalgene «Åpne» (alle saker som ikke er avsluttet, også vedtatte), «Vedtatt» og «Avsluttet» med antall, med en kort forklaring av hva «Åpne» betyr. Søk (saksnummer som `#12` eller `12`, tittel og beskrivelse) og en lukket del «Flere filtre» med status, prioritet, boenhet (inkludert «Hele sameiet») og sortering. Å velge en bestemt status der erstatter hurtigvalget (alle statuser kan nås, også «Alle (også avsluttede)»). Aktive filtre vises som merker som kan fjernes, «Viser x av y styresaker» står alltid over listen, og en tom liste forklarer at saker finnes. Raden viser sakstittel (lenke) med «Høy prioritet» som merke, hva saken gjelder (boenhet som lenke eller «Hele sameiet»), registrert møtedato («Ingen møtedato» hvis ingen) og status. Saksnummer, prioritet (unntatt Høy), tidsstempler og knapper per rad vises ikke. Standard sortering er møtedato (nærmeste først, saker uten dato sist) for «Åpne» og sist oppdatert for de andre visningene; alle sorteringer finnes fortsatt. Tabellen bruker `table-cards` på smal skjerm. Hovedhandlingen er «Ny styresak».
- **Skjema (registrer og rediger):** tittel (obligatorisk), beskrivelse, prioritet, møtedato, tilknyttet boenhet og merknader. Boenhetsvalget har «Hele sameiet (ingen boenhet)» og aktive boenheter. Ved redigering vises en senere deaktivert, koblet boenhet fortsatt som valgt og merket «(inaktiv)»; andre inaktive boenheter tilbys ikke. Feil vises ved feltet på norsk, og verdiene beholdes.
- **Detaljside (R3):** øverst tittel og handlinger: «Rediger», «Flere handlinger» (alle andre statuser som «Sett status til …»; lukkes etter bruk) og én hovedhandling som følger saken: «Start behandling» (Ny), «Registrer vedtak» (Under behandling uten vedtak), «Marker som vedtatt» (Under behandling med vedtak), «Avslutt saken» (Vedtatt) og «Gjenåpne saken» (Avsluttet; gir «Under behandling», og meldingen sier det). Under: status (med «Høy prioritet»), registrert møtedato (en dato registrert på saken, ikke en bekreftet møteinnkalling), «Gjelder» (boenhet som lenke eller «Hele sameiet») og beskrivelse. **Vedtak:** vedtaksteksten og «Vedtaksdato» står i en tydelig boks; mangler vedtak, står det at en sak bare kan settes til «Vedtatt» når et vedtak er registrert. «Registrer vedtak»/«Endre vedtak» åpner skjemaet (tekst, vedtaksdato, «Lagre vedtak», «Avbryt» og «Fjern vedtak»), og tekstfeltet får fokus. Reglene er uendret (Vedtatt krever vedtak, vedtak kan ikke fjernes mens saken er vedtatt). **Oppgaver til saken:** «(x av y fullført)», en kort liste med tittel (lenke), ansvarlig, frist og status, og «Ny oppgave» som foreslår saken (oppgaven kan fortsatt knyttes til en annen boenhet). Er alle oppgavene fullført og saken ikke avsluttet, står et diskret forslag om å vurdere avslutning; saken endres aldri automatisk. «Detaljer» (lukket, med oppsummering) har saksnummer, prioritet, merknader, opprettet og sist oppdatert. Bekreftelser og feil vises ved handlingen (i overskriften eller i vedtaksdelen). Sletting ligger adskilt nederst med bekreftelse. Datoene i listen og oversikten er bare registrerte møtedatoer; det finnes ingen møtemodell.
- **Sletting:** som for boenheter: knapp i et eget kort, bekreftelse med sakens tittel og «Dette kan ikke angres», Avbryt endrer ingenting, og melding i oversikten etter sletting. Hvis saken er knyttet til oppgaver blokkeres sletting med en forståelig melding (via `BoardCaseInUseException`; testet mot ekte oppgaver i M3C).
- **Boenhetens detaljside** har en seksjon «Styresaker» med saksnummer, tittel (lenke) og status, eller en kort tomtilstand. For aktive boenheter har seksjonen knappen «Registrer styresak», som åpner registreringssiden med boenheten forhåndsvalgt (`?boenhet=`). Inaktive boenheter har ikke knappen. Registreringssiden velger bare en boenhet som finnes og er aktiv, og viser ellers en gul merknad.

## Oppgaver (M3D)
- **Oversikt (R2):** hurtigfanene «Åpne», «Forfalte» og «Fullførte» med antall (Åpne = ikke startet og pågår; Forfalte = ikke fullførte med frist før i dag; Fullførte = status Fullført), ett søkefelt (oppgavenummer som `#12` eller `12`, tittel, beskrivelse og ansvarlig) og en lukket del «Flere filtre» med status, kategori, prioritet, boenhet, styresak og sortering. Å velge en bestemt status der erstatter hurtigvalget («Følger valgt visning» går tilbake). Aktive filtre vises som merker som kan fjernes, og «Viser x av y oppgaver» står alltid over listen. Er listen tom, står det at sameiet har oppgaver og hvilke valg som skjuler dem, med «Nullstill søk og filtre». Når alle oppgaver er fullført, tilbys «Vis også fullførte». Hovedhandlingen er «Ny oppgave».
- **Sortering:** «Frist (nærmeste først)» er standard: ikke fullførte oppgaver med nærmeste frist først (forfalte havner dermed øverst), deretter oppgaver uten frist (nyeste først), og fullførte sist. Dessuten nyeste først, eldste først, prioritet (høyest først, så nærmeste frist) og tittel.
- **Rader:** tittel (lenke til oppgaven; «Høy prioritet» som merke), under den hva oppgaven gjelder (styresak og/eller boenhet som lenker, ellers «Hele sameiet»), ansvarlig («Ikke tildelt» hvis ingen), frist med «Forfalt»/«Frist i dag», status og én handling: «Fullfør» (eller «Gjenåpne» for fullførte). Kategori, normal prioritet og oppgavenummer vises ikke i listen. Tabellen bruker `table-cards`: på smal skjerm (under 576 px) blir hver rad et kort med ansvarlig, frist og status under tittelen og uten vannrett rulling.
- **Frist og forfall:** «Forfalt» og «Frist i dag» vises som tekst i et merke (farge er bare et hint). Fullførte oppgaver er aldri forfalt. «I dag» er norsk kalenderdato (`TaskItemService.GetToday()`), også nær midnatt. Forfall beregnes med `TaskItem.IsOverdue` og lagres ikke.
- **Skjema (registrer og rediger):** tittel (obligatorisk), beskrivelse, kategori, prioritet, frist (datofelt), ansvarlig, boenhet, styresak og merknader. Feilmeldinger vises ved riktig felt via `ArgumentException.ParamName`. Knappen deaktiveres mens lagring pågår, så dobbel innsending ikke lager to oppgaver. Etter registrering vises detaljsiden med «Oppgaven er registrert.»
- **Koblinger:** boenhetsvalget har «Ingen boenhet» og aktive boenheter; ved redigering vises en senere deaktivert, allerede koblet boenhet som «(inaktiv)», men andre inaktive enheter tilbys ikke. Styresaksvalget har alle styresaker, også avsluttede. Begge kan fjernes. Boenhet og styresak trenger ikke høre sammen, og ingenting kopieres automatisk.
- **Detaljside (R2):** øverst tittel og handlinger: «Rediger», «Flere handlinger» (sett status til «Ikke startet» eller «Pågår»; lukkes etter bruk) og hovedhandlingen «Marker som fullført» (eller «Gjenåpne oppgaven»). Under: status (med «Høy prioritet»), frist med forfallsmerke, ansvarlig, «Gjelder» (boenhet som lenke eller «Hele sameiet»), styresak som lenke, og beskrivelsen. «Detaljer» (lukket, med oppsummering) inneholder oppgavenummer, kategori, prioritet, merknader, opprettet, sist oppdatert og fullført-tidspunkt. Bekreftelser og feil vises rett under overskriften.
- **Fullføring og angring:** fullføring fra listen eller detaljsiden går via `TaskItemService.ChangeStatusAsync`. Raden blir stående på samme plass med meldingen ««Tittel» er fullført.» og «Angre» til personen endrer visningen. «Angre» setter oppgaven tilbake til statusen den hadde (Ikke startet eller Pågår) og sier det uttrykkelig; det er ingen ny domeneregel, bare en ny statusendring (oppdatert-tidspunktet endres). «Gjenåpne» setter statusen til «Pågår» og meldingen sier det; den har ingen «Angre» (oppgaven kan fullføres igjen). Knappen er deaktivert mens lagring pågår (dobbeltklikk gir én endring). Feil vises ved oppgaven (`role=alert`) og forsvinner ikke før de lukkes.
- **Sletting:** som for boenheter og styresaker (eget kort nederst, bekreftelse med tittel og «Dette kan ikke angres», Avbryt endrer ingenting, melding «Oppgaven er slettet permanent.» på oversikten). Teksten sier at styresaken og boenheten ikke slettes.
- **Snarveier:** styresakens og boenhetens detaljside har en seksjon «Oppgaver» (nummer, tittel, status, frist) med knappen «Registrer oppgave» som åpner registreringen med styresaken (`?styresak=`) eller boenheten (`?boenhet=`) forhåndsvalgt. Boenhetens knapp vises bare for aktive boenheter, men eksisterende oppgaver på en inaktiv boenhet vises fortsatt. Verdiene i URL-en kontrolleres mot de faktiske dataene (ugyldig, ukjent eller inaktiv verdi gir en melding og ingen forhåndsvalg).

## Oversikt (M4, redesignet i M5D-R1)
Forsiden (`/`) heter «Oversikt» og svarer på ett spørsmål: hva må styret følge opp nå? Rekkefølgen er bevisst:
- **Beskjeder:** resultatet av en gjenoppretting (`RestoreResultNotice`) og feil står øverst og er aldri skjult i en meny. Kan data ikke leses, vises en feilmelding med «Prøv igjen» og ingen tall.
- **«+ Ny»:** hovedhandlingen øverst til høyre (`MoreMenu` med knappestil), med lenker til «Ny oppgave», «Ny styresak» og «Ny boenhet».
- **Tre tall (lenker):** «Forfalte oppgaver» (`/oppgaver?forfalt=1`, rød kant bare når tallet er over 0), «Åpne oppgaver» (`/oppgaver`) og «Åpne styresaker» (`/styresaker`). Definisjonene er de samme som i oppgavelisten og styresaklisten (`DashboardOverview`). Antall boenheter vises ikke lenger her.
- **Må følges opp:** inntil 5 oppgaver fra `DashboardOverview.FollowUpTasks` (forfalte først, så frist i dag, så frister de neste 7 dagene). Hver rad viser tittel (lenke), ansvarlig («Ikke tildelt» hvis ingen), frist og «Forfalt»/«Frist i dag». Er det flere enn 5, står «Viser 5 av n oppgaver som haster». Andre åpne oppgaver fylles ikke inn, slik at lista bare viser det som haster. «Se alle oppgaver» åpner hele listen.
- **Kommende saker med møtedato:** inntil 5 åpne (ikke avsluttede) styresaker med møtedato i dag eller senere, nærmeste dato først, med dato, tittel (lenke) og status. En kort forklaring sier at datoen er møtedatoen som er registrert på saken, og at den ikke betyr at et møte er bekreftet eller innkalt. Seksjonen heter bevisst ikke «Neste styremøte»: SameieHub har ingen møtemodell. Den kan senere erstattes av eller bygges sammen med egen møteadministrasjon.
- **Elbillading:** en kort statuslinje (siste avstemming med differanse, eller siste avlesning av hovedmåleren hvis det ikke finnes avstemming) og lenkene «Elbillading» og «Registrer avlesning». Beregningene er uendret.
- **Førstegangsbruk:** er det ikke registrert noen boenheter, styresaker eller oppgaver, vises «Kom i gang» i stedet for nuller: boenhetene, styresaker (med forklaring) og oppgaver, med lenker, og en lenke til sikkerhetskopi. Så snart noe er registrert, forsvinner veiledningen, og tomme seksjoner forklarer hva man gjør.
- **Fjernet fra forsiden:** tallkortet «Aktive boenheter», «Aktuelle styresaker» (erstattet av «Kommende saker med møtedato»), «Kommende datoer», «Hurtighandlinger» (erstattet av «+ Ny») og knappen «Oppdater oversikten» (siden hentes på nytt hver gang den åpnes).
- **Smal skjerm:** alt ligger i én kolonne; oppgaver vises som lister (tittel, så ansvarlig, frist og forfallsmerke), uten vannrett rulling. Menyvalget i sidemenyen heter også «Oversikt».

## Elbillading (M4E-C)
- **Oversikt:** tall per status, hovedmåler med siste avlesning, siste avstemming og en tabell over ladere (boenhet, skap, status, siste avlesning). Boenheter uten lader vises med lenke til registrering. Uten anlegg vises et oppsettskjema.
- **Samlet avlesning:** ett skjema med tidspunkt, hovedmåler og de åtte undermålerne gruppert i sikringsskap 1 og 2. Ved hvert felt står forrige målerstand og forbruket siden forrige. Tomt felt betyr «ikke avlest» og lagres ikke; 0 er en ekte verdi. Alt lagres i én transaksjon, og feil vises ved riktig felt uten at noe lagres. Etter lagring vises en bekreftelse med forbruk per måler og eventuelle målere som ikke ble avlest.
- **Norsk tid:** brukeren skriver norsk lokal tid (`datetime-local`). Tiden lagres som UTC. Klokkeslettet som ikke fantes (vår) avvises med forklaring, og klokkeslettet som forekommer to ganger (høst) krever at brukeren velger sommer- eller vintertid.
- **Avstemming:** perioden velges blant eksisterende avlesninger av hovedmåleren (ingen fri datovelger). Siden viser start og slutt, hovedmålerens forbruk, summen av undermålerne, differanse i kWh og prosent og en tabell per måler med de faktiske avlesningstidene. Fullstendig krever hovedmåler og åtte undermålere med dokumentert forbruk. Ellers står det hva som mangler, og differansen vises ikke. Toleransen på 24 timer er forklart, og tilnærmede avlesninger er merket.
- **Historikk, korrigering og sletting:** per måler, nyeste først, med forbruk siden forrige. Korrigering må passe mellom nabo-avlesningene. Sletting krever bekreftelse (`role="alertdialog"`).
- **Boenhet og dashboard:** boenhetssiden har en kompakt seksjon «Elbillader», og oversikten en kort statuslinje med siste avstemming og lenker til «Elbillading» og «Registrer avlesning».
- **Smal skjerm:** verifisert i nettleser på 375 px: ingen vannrett rulling på noen elbillingside.

## Sikkerhetskopi (M5C)
Én side med to atskilte kort: «Lag sikkerhetskopi» og «Gjenopprett fra sikkerhetskopi». Ingen tekniske begreper (ingen migreringer, sjekksummer eller SQLite). Knappene deaktiveres mens en handling pågår. Filvalg (`InputFile`) viser en kontroll med forskjell på «nå» og «i sikkerhetskopien», en bekreftelsesboks (`role="alertdialog"`) og en forklaring på hva brukeren må gjøre ved omstart. Resultatet vises som en beskjed på forsiden og på siden. Interne banenavn vises aldri. Se [ARCHITECTURE.md](ARCHITECTURE.md), «Sikkerhetskopi og gjenoppretting i brukergrensesnittet (M5C)».

## Ikke laget ennå
Endringshistorikk for styresaker.
