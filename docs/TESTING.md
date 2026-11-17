# Testing

- **Rammeverk:** xUnit (`tests/SameieHub.Tests`).
- **TDD** for forretningslogikk: test først → rød av riktig årsak → implementer → grønn → refaktorer.
- **Enhetstester:** Domain-regler (`Domain/HousingUnitTests`: adresse og `AssignId`; `Domain/HousingUnitServiceTests`: tjenesten mot `Fakes/FakeHousingUnitRepository`, inkludert Id-konsistens, ukjent Id og uendret adresse ved feil), med fake repositories (enkle in-memory-klasser) der interfaces finnes.
- **Integrasjonstester:** Infrastructure mot ekte SQLite (in-memory eller temp-fil), aldri mot brukerens data.
- **Arkitekturtest:** `DependencyDirectionTests` vokter avhengighetsretningen.
- **Kjøring:** `dotnet test SameieHub.slnx`. CI kjører restore, build og test ved push til `main` og pull requests.
- **Navnekonvensjon:** `Metode_Scenario_ForventetResultat`.
