using SameieHub.Domain;

namespace SameieHub.Tests.Infrastructure;

/// <summary>Fictional data in every module, including values that must survive a backup and a restore exactly.</summary>
internal static class DatabaseTestData
{
    public const string OwnerName = "Fiktiv Eier";
    public const string OwnerPhone = "+47 000 00 999";
    public const string OwnerEmail = "fiktiv.eier@example.com";
    public const string AssignedTo = "Fiktiv Ansvarlig";

    public static async Task FillAllModulesAsync(SqliteTestDatabase database)
    {
        var unit = await database.Service.RegisterAsync(new HousingUnitDetails
        {
            Address = "Fiktivdalen 10A",
            UnitNumber = "10A",
            SectionNumber = 1,
            HousingType = HousingType.Duplex,
            AreaSquareMeters = 118.5m,
            OwnerName = OwnerName,
            OwnerPhoneNumber = OwnerPhone,
            OwnerEmail = OwnerEmail,
            Notes = "Æøå i merknaden"
        });
        var boardCase = await database.BoardCases.RegisterAsync(new BoardCaseDetails { Title = "Sak med vedtak", HousingUnitId = unit.Id });
        await database.BoardCases.RegisterDecisionAsync(boardCase.Id, "Vedtatt av styret", new DateOnly(2026, 11, 10));
        await database.Tasks.RegisterAsync(new TaskItemDetails { Title = "Oppgave", HousingUnitId = unit.Id, BoardCaseId = boardCase.Id, AssignedTo = AssignedTo });

        var setup = await ChargingSetup.CreateAsync(database);
        await database.Charging.RegisterReadingSetAsync(setup.Set(60, 0m, ChargingSetup.Same(0m)));
        await database.Charging.RegisterReadingSetAsync(setup.Set(30, 0.001m, ChargingSetup.Same(0.001m)));
        await database.Charging.RegisterReadingSetAsync(setup.Set(1, 12345.678m, ChargingSetup.Same(1234.5m)));
    }
}
