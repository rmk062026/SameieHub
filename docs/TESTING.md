# Testing

- **Rammeverk:** xUnit (`tests/SameieHub.Tests`).
- **TDD** for forretningslogikk: test først → rød av riktig årsak → implementer → grønn → refaktorer.
- **Enhetstester:** Domain-regler (`Domain/HousingUnitTests`: adresse og `AssignId`; `Domain/HousingUnitServiceTests`: tjenesten mot `Fakes/FakeHousingUnitRepository`, inkludert Id-konsistens, ukjent Id og uendret adresse ved feil), med fake repositories (enkle in-memory-klasser) der interfaces finnes.
- **Integrasjonstester:** `Infrastructure/EfHousingUnitRepositoryTests` kjører mot ekte SQLite (ikke EF InMemory). `SqliteTestDatabase` lager en temp-fil per test via de samme migreringene som appen, og sletter den etterpå. Brukerens database i AppData berøres aldri. `RestartAsync()` simulerer omstart mot samme fil.
- **UI-tester:** `Web/` bruker bUnit mot Razor-komponentene med fake repository (`FailableHousingUnitRepository` kan simulere databasefeil). Dekker tomtilstand, sortering, antall, registrering, redigering, validering, feil og ukjent Id. Ikke dekket av bUnit: sidene leses fra spørringsstrengen (`?status=`, bekreftet manuelt), rendermodus og ekte tastatur/nettleser.
- **Manuell UI-kontroll (M1D):** appen ble startet mot en isolert database og styrt i nettleser: tom tilstand, validering, registrering med Enter, redigering, omstart med data intakt, mobilvisning og menyknapp.
- **Arkitekturtest:** `DependencyDirectionTests` vokter avhengighetsretningen.
- **Kjøring:** `dotnet test SameieHub.slnx`. CI kjører restore, build og test ved push til `main` og pull requests.
- **Navnekonvensjon:** `Metode_Scenario_ForventetResultat`.
