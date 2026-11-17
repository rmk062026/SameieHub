# Testing

- **Rammeverk:** xUnit (`tests/SameieHub.Tests`).
- **TDD** for forretningslogikk: test først → rød av riktig årsak → implementer → grønn → refaktorer.
- **Enhetstester:** Domain-regler (`Domain/HousingUnitTests`: adresse og `AssignId`; `Domain/HousingUnitServiceTests`: tjenesten mot `Fakes/FakeHousingUnitRepository`, inkludert Id-konsistens, ukjent Id og uendret adresse ved feil), med fake repositories (enkle in-memory-klasser) der interfaces finnes.
- **Integrasjonstester:** `Infrastructure/EfHousingUnitRepositoryTests` kjører mot ekte SQLite (ikke EF InMemory). `SqliteTestDatabase` lager en temp-fil per test via de samme migreringene som appen, og sletter den etterpå. Brukerens database i AppData berøres aldri. `RestartAsync()` simulerer omstart mot samme fil.
- **Arkitekturtest:** `DependencyDirectionTests` vokter avhengighetsretningen.
- **Kjøring:** `dotnet test SameieHub.slnx`. CI kjører restore, build og test ved push til `main` og pull requests.
- **Navnekonvensjon:** `Metode_Scenario_ForventetResultat`.
