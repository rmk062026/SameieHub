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
    Dashboard.razor, BoardCases.razor, Tasks.razor   (plassholdere)
    HousingUnits/  HousingUnitList, HousingUnitDetail, HousingUnitCreate, HousingUnitEdit,
                   HousingUnitForm, HousingUnitFormModel, HousingUnitFieldMessages,
                   HousingUnitStatusBadge, HousingTypeText
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
| `/styresaker`, `/oppgaver` | Plassholdere | Ikke laget |

Sidemeny på bred skjerm (≥ 768 px); på smal skjerm en toppstripe med menyknapp.

## Stil
- Farger og radius er CSS-variabler i `:root` i `app.css` (`--sh-primary`, `--sh-sidebar`, …). Endre der for å endre hele designet.
- Layout-stiler ligger i `*.razor.css` (Blazor CSS-isolasjon); felles stiler (knapper, kort, tabell, skjema) i `app.css`.
- Ikoner er små inline SVG-er i `NavMenu.razor`.

## Boenheter
- **Oversikt:** tre tall (registrerte, aktive, inaktive), søkefelt, filter Aktive/Inaktive/Alle (standard: Aktive) og tabell med adresse (og bolignummer), eier (begge navn, «Navn 1 og Navn 2»), seksjon, status og handlingene Åpne/Rediger. Sortert på adresse med tall i naturlig rekkefølge («2A» før «10B»). På smal skjerm skjules eier- og seksjonskolonnen, og eiernavn vises under adressen. Egne tomtilstander for tom database og for ingen treff («Nullstill søk og filter»), pluss lastetilstand og feilmelding med «Prøv igjen».
- **Detaljvisning:** kort for boliginformasjon, eier og kontakt, og merknader. Manglende valgfrie felt vises som «Ikke registrert». Eier 2 vises bare når den er registrert (da merkes eierne «Eier 1»/«Eier 2»). Telefon og e-post er lenker. Knappene Rediger og Deaktiver/Reaktiver ligger i toppen, med tilbakemelding etter statusendring.
- **Skjema (registrer og rediger):** felles `HousingUnitForm` med tre grupper: Boliginformasjon, Eier og kontaktinformasjon, Andre opplysninger. Passende inputtyper (tall, `tel`, `email`, nedtrekksliste, flerlinjet tekst). Eier 2 skjules bak «+ Legg til eier 2» og kan fjernes med «Fjern eier 2» (fjerner bare eier 2). Feil vises ved feltet på norsk, verdiene beholdes, og knappen låses mens lagring pågår.
- **Etter lagring:** brukeren sendes til detaljsiden med melding (`?status=opprettet` / `?status=endret`).
- **Status:** «Aktiv»/«Inaktiv» vises som tekst i en farget merkelapp (fargen er bare en hjelp).
- **Rendermodus:** Interactive Server uten prerendering, slik at data hentes én gang per side.

## Ikke laget ennå
Hard sletting av boenheter (bevisst utelatt, se [REQUIREMENTS.md](REQUIREMENTS.md)).
