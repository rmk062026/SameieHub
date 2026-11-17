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
  Shared/    PageHeader (tittel, beskrivelse, handlinger), ComingSoon (plassholder)
  Pages/
    Dashboard.razor, BoardCases.razor, Tasks.razor   (plassholdere)
    HousingUnits/  HousingUnitList, HousingUnitCreate, HousingUnitEdit,
                   HousingUnitForm, HousingUnitFormModel
wwwroot/app.css  designvariabler og felles stiler
```

## Navigasjon og ruter
| Rute | Side | Status |
|---|---|---|
| `/` | Dashboard | Plassholder |
| `/boenheter` | Oversikt | Funksjonell |
| `/boenheter/ny` | Registrer | Funksjonell |
| `/boenheter/{id}/rediger` | Endre adresse | Funksjonell |
| `/styresaker`, `/oppgaver` | Plassholdere | Ikke laget |

Sidemeny på bred skjerm (≥ 768 px); på smal skjerm en toppstripe med menyknapp.

## Stil
- Farger og radius er CSS-variabler i `:root` i `app.css` (`--sh-primary`, `--sh-sidebar`, …). Endre der for å endre hele designet.
- Layout-stiler ligger i `*.razor.css` (Blazor CSS-isolasjon); felles stiler (knapper, kort, tabell, skjema) i `app.css`.
- Ikoner er små inline SVG-er i `NavMenu.razor`.

## Boenheter
- **Oversikt:** antall, tabell (ID, adresse, «Rediger»), sortert på adresse med tall i naturlig rekkefølge («2A» før «10B»), tomtilstand, lastetilstand og feilmelding med «Prøv igjen».
- **Skjema:** felles `HousingUnitForm`. Feltvalidering («Adressen må fylles ut.») ved feltet, verdien beholdes ved feil, knappen er låst mens lagring pågår, og domenefeil og databasefeil vises som forståelig melding. Domenet er fortsatt den egentlige validatoren.
- **Tilbakemelding:** etter lagring går brukeren til oversikten med en melding (`?status=opprettet` / `?status=endret`).
- **Rendermodus:** Interactive Server uten prerendering, slik at data hentes én gang per side.

## Ikke laget ennå
Sletting, søk/filter og eier/kontaktinformasjon for boenheter. Se «Gjenstår» i [REQUIREMENTS.md](REQUIREMENTS.md).
