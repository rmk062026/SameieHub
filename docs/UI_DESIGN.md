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
wwwroot/app.css  designvariabler og felles stiler
```

## Navigasjon og ruter
| Rute | Side | Status |
|---|---|---|
| `/` | Dashboard | Plassholder |
| `/boenheter` | Oversikt | Funksjonell |
| `/boenheter/ny` | Registrer | Funksjonell |
| `/boenheter/{id}` | Detaljvisning | Funksjonell |
| `/boenheter/{id}/rediger` | Rediger alle opplysninger | Funksjonell |
| `/styresaker` | Oversikt | Funksjonell |
| `/styresaker/ny` | Registrer | Funksjonell |
| `/styresaker/{id}` | Detaljvisning med status og vedtak | Funksjonell |
| `/styresaker/{id}/rediger` | Rediger | Funksjonell |
| `/oppgaver` | Plassholder | Ikke laget |

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
- **Oversikt:** fem statistikkort (totalt og per status), søk (saksnummer som `#12` eller `12`, tittel og beskrivelse), filter på status, prioritet og boenhet (inkludert «Hele sameiet»), sortering (nyeste først er standard; eldste, sist oppdatert, møtedato, prioritet, tittel) og en tabell med saksnummer, tittel (og boenhet), status, prioritet, opprettet og sist oppdatert. Bred skjerm viser alle kolonner; smal skjerm viser sak, tittel, status og handlinger. Egne tomtilstander for ingen saker og ingen treff, lastetilstand og feilmelding med «Prøv igjen».
- **Skjema (registrer og rediger):** tittel (obligatorisk), beskrivelse, prioritet, møtedato, tilknyttet boenhet og merknader. Boenhetsvalget har «Hele sameiet (ingen boenhet)» og aktive boenheter. Ved redigering vises en senere deaktivert, koblet boenhet fortsatt som valgt og merket «(inaktiv)»; andre inaktive boenheter tilbys ikke. Feil vises ved feltet på norsk, og verdiene beholdes.
- **Detaljside:** saksinformasjon, beskrivelse, status (nedtrekksliste med «Endre status» og «Gjenåpne saken» for avsluttede saker), vedtak (registrer, korriger, fjern) med vedtaksdato (felt og en tydelig boks som viser teksten og «Vedtaksdato: dd.MM.yyyy» eller «Ikke registrert»), merknader og «Slett styresak». Boenheten er en lenke til boenhetens side, også når den er inaktiv. Tider vises i norsk tid (`dd.MM.yyyy HH:mm`); lagringsformatet er UTC.
- **Sletting:** som for boenheter: knapp i et eget kort, bekreftelse med sakens tittel og «Dette kan ikke angres», Avbryt endrer ingenting, og melding i oversikten etter sletting. Hvis saken er knyttet til oppgaver blokkeres sletting med en forståelig melding (via `BoardCaseInUseException`; kan først testes mot ekte oppgaver i M3).
- **Boenhetens detaljside** har en seksjon «Styresaker» med saksnummer, tittel (lenke) og status, eller en kort tomtilstand. For aktive boenheter har seksjonen knappen «Registrer styresak», som åpner registreringssiden med boenheten forhåndsvalgt (`?boenhet=`). Inaktive boenheter har ikke knappen. Registreringssiden velger bare en boenhet som finnes og er aktiv, og viser ellers en gul merknad.

## Ikke laget ennå
Oppgaver og dashboard. Endringshistorikk for styresaker.
