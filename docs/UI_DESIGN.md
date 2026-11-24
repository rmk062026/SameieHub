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
| `/` | Dashboard | Funksjonell |
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
- **Oversikt (R2):** hurtigfanene «Åpne», «Forfalte» og «Fullførte» med antall (Åpne = ikke startet og pågår; Forfalte = ikke fullførte med frist før i dag; Fullførte = status Fullført), ett søkefelt (oppgavenummer som `#12` eller `12`, tittel, beskrivelse og ansvarlig) og en lukket del «Flere filtre» med status, kategori, prioritet, boenhet, styresak og sortering. Å velge en bestemt status der erstatter hurtigvalget («Følger valgt visning» går tilbake). Aktive filtre vises som merker som kan fjernes, og «Viser x av y oppgaver» står alltid over listen. Er listen tom, står det at sameiet har oppgaver og hvilke valg som skjuler dem, med «Nullstill søk og filtre». Når alle oppgaver er fullført, tilbys «Vis også fullførte». Hovedhandlingen er «Ny oppgave».
- **Skjema (registrer og rediger):** tittel (obligatorisk), beskrivelse, prioritet, møtedato, tilknyttet boenhet og merknader. Boenhetsvalget har «Hele sameiet (ingen boenhet)» og aktive boenheter. Ved redigering vises en senere deaktivert, koblet boenhet fortsatt som valgt og merket «(inaktiv)»; andre inaktive boenheter tilbys ikke. Feil vises ved feltet på norsk, og verdiene beholdes.
- **Detaljside:** saksinformasjon, beskrivelse, status (nedtrekksliste med «Endre status» og «Gjenåpne saken» for avsluttede saker), vedtak (registrer, korriger, fjern) med vedtaksdato (felt og en tydelig boks som viser teksten og «Vedtaksdato: dd.MM.yyyy» eller «Ikke registrert»), merknader og «Slett styresak». Boenheten er en lenke til boenhetens side, også når den er inaktiv. Tider vises i norsk tid (`dd.MM.yyyy HH:mm`); lagringsformatet er UTC.
- **Sletting:** som for boenheter: knapp i et eget kort, bekreftelse med sakens tittel og «Dette kan ikke angres», Avbryt endrer ingenting, og melding i oversikten etter sletting. Hvis saken er knyttet til oppgaver blokkeres sletting med en forståelig melding (via `BoardCaseInUseException`; testet mot ekte oppgaver i M3C).
- **Boenhetens detaljside** har en seksjon «Styresaker» med saksnummer, tittel (lenke) og status, eller en kort tomtilstand. For aktive boenheter har seksjonen knappen «Registrer styresak», som åpner registreringssiden med boenheten forhåndsvalgt (`?boenhet=`). Inaktive boenheter har ikke knappen. Registreringssiden velger bare en boenhet som finnes og er aktiv, og viser ellers en gul merknad.

## Oppgaver (M3D)
- **Oversikt:** fem statistikkort (totalt, ikke startet, pågår, fullført, forfalt), søk (oppgavenummer som `#12` eller `12`, tittel, beskrivelse og ansvarlig), filter på status, kategori, prioritet, boenhet, styresak og «Bare forfalte», og sortering. **Standardvisningen viser oppgaver som ikke er fullført**; statusvalget har «Ikke fullførte» (standard), «Alle», «Ikke startet», «Pågår» og «Fullførte». Når alle oppgaver er fullført, forklarer tomtilstanden det og tilbyr «Vis også fullførte».
- **Sortering:** «Frist (nærmeste først)» er standard: ikke fullførte oppgaver med nærmeste frist først (forfalte havner dermed øverst), deretter oppgaver uten frist (nyeste først), og fullførte sist. Dessuten nyeste først, eldste først, prioritet (høyest først, så nærmeste frist) og tittel.
- **Rader:** tittel (lenke til oppgaven; «Høy prioritet» som merke), under den hva oppgaven gjelder (styresak og/eller boenhet som lenker, ellers «Hele sameiet»), ansvarlig («Ikke tildelt» hvis ingen), frist med «Forfalt»/«Frist i dag», status og én handling: «Fullfør» (eller «Gjenåpne» for fullførte). Kategori, normal prioritet og oppgavenummer vises ikke i listen. Tabellen bruker `table-cards`: på smal skjerm (under 576 px) blir hver rad et kort med ansvarlig, frist og status under tittelen og uten vannrett rulling.
- **Frist og forfall:** «Forfalt» og «Frist i dag» vises som tekst i et merke (farge er bare et hint). Fullførte oppgaver er aldri forfalt. «I dag» er norsk kalenderdato (`TaskItemService.GetToday()`), også nær midnatt. Forfall beregnes med `TaskItem.IsOverdue` og lagres ikke.
- **Skjema (registrer og rediger):** tittel (obligatorisk), beskrivelse, kategori, prioritet, frist (datofelt), ansvarlig, boenhet, styresak og merknader. Feilmeldinger vises ved riktig felt via `ArgumentException.ParamName`. Knappen deaktiveres mens lagring pågår, så dobbel innsending ikke lager to oppgaver. Etter registrering vises detaljsiden med «Oppgaven er registrert.»
- **Koblinger:** boenhetsvalget har «Ingen boenhet» og aktive boenheter; ved redigering vises en senere deaktivert, allerede koblet boenhet som «(inaktiv)», men andre inaktive enheter tilbys ikke. Styresaksvalget har alle styresaker, også avsluttede. Begge kan fjernes. Boenhet og styresak trenger ikke høre sammen, og ingenting kopieres automatisk.
- **Detaljside (R2):** øverst tittel og handlinger: «Rediger», «Flere handlinger» (sett status til «Ikke startet» eller «Pågår»; lukkes etter bruk) og hovedhandlingen «Marker som fullført» (eller «Gjenåpne oppgaven»). Under: status (med «Høy prioritet»), frist med forfallsmerke, ansvarlig, «Gjelder» (boenhet som lenke eller «Hele sameiet»), styresak som lenke, og beskrivelsen. «Detaljer» (lukket, med oppsummering) inneholder oppgavenummer, kategori, prioritet, merknader, opprettet, sist oppdatert og fullført-tidspunkt. Bekreftelser og feil vises rett under overskriften.
- **Fullføring og angring:** fullføring fra listen eller detaljsiden går via `TaskItemService.ChangeStatusAsync`. Raden blir stående på samme plass med meldingen ««Tittel» er fullført.» og «Angre» til personen endrer visningen. «Angre» setter oppgaven tilbake til statusen den hadde (Ikke startet eller Pågår) og sier det uttrykkelig; det er ingen ny domeneregel, bare en ny statusendring (oppdatert-tidspunktet endres). «Gjenåpne» setter statusen til «Pågår» og meldingen sier det; den har ingen «Angre» (oppgaven kan fullføres igjen). Knappen er deaktivert mens lagring pågår (dobbeltklikk gir én endring). Feil vises ved oppgaven (`role=alert`) og forsvinner ikke før de lukkes.
- **Sletting:** som for boenheter og styresaker (eget kort nederst, bekreftelse med tittel og «Dette kan ikke angres», Avbryt endrer ingenting, melding «Oppgaven er slettet permanent.» på oversikten). Teksten sier at styresaken og boenheten ikke slettes.
- **Snarveier:** styresakens og boenhetens detaljside har en seksjon «Oppgaver» (nummer, tittel, status, frist) med knappen «Registrer oppgave» som åpner registreringen med styresaken (`?styresak=`) eller boenheten (`?boenhet=`) forhåndsvalgt. Boenhetens knapp vises bare for aktive boenheter, men eksisterende oppgaver på en inaktiv boenhet vises fortsatt. Verdiene i URL-en kontrolleres mot de faktiske dataene (ugyldig, ukjent eller inaktiv verdi gir en melding og ingen forhåndsvalg).

## Dashboard (M4)
- **Statistikkkort (lenker):** aktive boenheter (`/boenheter`), åpne styresaker (`/styresaker`), åpne oppgaver (`/oppgaver`) og forfalte oppgaver (`/oppgaver?forfalt=1`, som åpner oppgavelisten med hurtigvalget «Forfalte»). Forfalt-kortet får rød kant og rødt tall når det finnes forfalte oppgaver; tallet står alltid også som tekst.
- **Oppgaver som krever oppfølging:** forfalte oppgaver først, så frist i dag, så frister de neste 7 dagene, maks 8. Nr., tittel (lenke), prioritet (fra bred skjerm), status og frist med «Forfalt»/«Frist i dag». Knappen «Se alle oppgaver» åpner hele listen.
- **Aktuelle styresaker:** åpne saker (ikke avsluttet), sist oppdaterte først, maks 5, med saksnummer, tittel (lenke), status, prioritet (fra bred skjerm) og møtedato eller «Ingen møtedato». Knappen «Se alle styresaker» åpner hele listen.
- **Kommende datoer:** styremøter (møtedato på åpne saker) og oppgavefrister de neste 60 dagene, sortert på dato, maks 8, med dato, type («Styremøte»/«Frist») og lenke. Oppgaver som allerede står i oppfølgingslisten (frist innen 7 dager) gjentas ikke.
- **Hurtighandlinger:** vanlige lenker til `/boenheter/ny`, `/styresaker/ny` og `/oppgaver/ny`.
- **Oppdatering og feil:** «Oppdater oversikten» henter tallene på nytt. Kan data ikke leses, vises en feilmelding med «Prøv igjen», og ingen tall (ikke 0). Ingen timere eller sanntid.
- **Tom database:** alle kort viser 0 (reelle tall), og hver seksjon har en tomtilstand: «Ingen åpne oppgaver. Registrer en oppgave …», «Ingen åpne styresaker.» og «Ingen kommende møter eller frister de neste 60 dagene.».
- **Smal skjerm:** kortene ligger to og to. Nr.-kolonnen skjules i tabellene (prioritet skjules under bred skjerm), så fristen alltid er synlig uten at siden får vannrett rulling.

## Elbillading (M4E-C)
- **Oversikt:** tall per status, hovedmåler med siste avlesning, siste avstemming og en tabell over ladere (boenhet, skap, status, siste avlesning). Boenheter uten lader vises med lenke til registrering. Uten anlegg vises et oppsettskjema.
- **Samlet avlesning:** ett skjema med tidspunkt, hovedmåler og de åtte undermålerne gruppert i sikringsskap 1 og 2. Ved hvert felt står forrige målerstand og forbruket siden forrige. Tomt felt betyr «ikke avlest» og lagres ikke; 0 er en ekte verdi. Alt lagres i én transaksjon, og feil vises ved riktig felt uten at noe lagres. Etter lagring vises en bekreftelse med forbruk per måler og eventuelle målere som ikke ble avlest.
- **Norsk tid:** brukeren skriver norsk lokal tid (`datetime-local`). Tiden lagres som UTC. Klokkeslettet som ikke fantes (vår) avvises med forklaring, og klokkeslettet som forekommer to ganger (høst) krever at brukeren velger sommer- eller vintertid.
- **Avstemming:** perioden velges blant eksisterende avlesninger av hovedmåleren (ingen fri datovelger). Siden viser start og slutt, hovedmålerens forbruk, summen av undermålerne, differanse i kWh og prosent og en tabell per måler med de faktiske avlesningstidene. Fullstendig krever hovedmåler og åtte undermålere med dokumentert forbruk. Ellers står det hva som mangler, og differansen vises ikke. Toleransen på 24 timer er forklart, og tilnærmede avlesninger er merket.
- **Historikk, korrigering og sletting:** per måler, nyeste først, med forbruk siden forrige. Korrigering må passe mellom nabo-avlesningene. Sletting krever bekreftelse (`role="alertdialog"`).
- **Boenhet og dashboard:** boenhetssiden har en kompakt seksjon «Elbillader», og dashboardet en liten seksjon med tall og siste avstemming.
- **Smal skjerm:** verifisert i nettleser på 375 px: ingen vannrett rulling på noen elbillingside.

## Sikkerhetskopi (M5C)
Én side med to atskilte kort: «Lag sikkerhetskopi» og «Gjenopprett fra sikkerhetskopi». Ingen tekniske begreper (ingen migreringer, sjekksummer eller SQLite). Knappene deaktiveres mens en handling pågår. Filvalg (`InputFile`) viser en kontroll med forskjell på «nå» og «i sikkerhetskopien», en bekreftelsesboks (`role="alertdialog"`) og en forklaring på hva brukeren må gjøre ved omstart. Resultatet vises som en beskjed på forsiden og på siden. Interne banenavn vises aldri. Se [ARCHITECTURE.md](ARCHITECTURE.md), «Sikkerhetskopi og gjenoppretting i brukergrensesnittet (M5C)».

## Ikke laget ennå
Endringshistorikk for styresaker.
