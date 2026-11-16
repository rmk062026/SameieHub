# UI-design (retning)

- Norsk brukergrensesnitt, enkel sidemeny: **Dashboard · Boenheter · Styresaker · Oppgaver**.
- Blazor-komponenter i `SameieHub.Web/Components`; sider per område under `Components/Pages/<Område>/`.
- Bootstrap (følger malen) for tabeller, skjemaer og knapper; konsekvent «liste → detalj/rediger»-mønster.
- Statuser vises med tydelige merker (badges); forfalte oppgaver fremheves.
- Validering vises ved feltet; destruktive handlinger krever bekreftelse.
- Universell utforming: tastaturnavigasjon, kontrast, labels på alle felt.
- Malens eksempelsider (Counter, Weather) fjernes når første ekte side lages i M1.
