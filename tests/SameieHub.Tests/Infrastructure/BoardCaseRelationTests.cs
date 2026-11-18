using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SameieHub.Domain;

namespace SameieHub.Tests.Infrastructure;

public class BoardCaseRelationTests : IAsyncLifetime
{
    private SqliteTestDatabase _database = null!;

    public async Task InitializeAsync()
    {
        _database = await SqliteTestDatabase.CreateAsync();
    }

    public async Task DisposeAsync()
    {
        await _database.DisposeAsync();
    }

    private static BoardCaseDetails Details(int? housingUnitId = null) =>
        new() { Title = "Maling av fellesarealer", HousingUnitId = housingUnitId };

    private async Task<HousingUnit> RegisterUnitAsync(string address = "Storgata 1A")
    {
        return await _database.Service.RegisterAsync(address);
    }

    // Schema

    [Fact]
    public async Task Schema_ForeignKeyToHousingUnits_IsRestrict()
    {
        var onDelete = await _database.QueryTextAsync(
            "SELECT on_delete FROM pragma_foreign_key_list('BoardCases') WHERE \"table\" = 'HousingUnits'");

        Assert.Equal("RESTRICT", onDelete);
    }

    // Linking

    [Fact]
    public async Task Register_WithoutHousingUnit_StoresNullLink()
    {
        var registered = await _database.BoardCases.RegisterAsync(Details());

        await _database.RestartAsync();

        Assert.Null((await _database.BoardCases.GetByIdAsync(registered.Id))!.HousingUnitId);
        Assert.Equal("1", await _database.QueryTextAsync("SELECT COUNT(*) FROM BoardCases WHERE HousingUnitId IS NULL"));
    }

    [Fact]
    public async Task Register_WithActiveHousingUnit_StoresTheLink()
    {
        var unit = await RegisterUnitAsync();

        var registered = await _database.BoardCases.RegisterAsync(Details(unit.Id));
        await _database.RestartAsync();

        Assert.Equal(unit.Id, (await _database.BoardCases.GetByIdAsync(registered.Id))!.HousingUnitId);
    }

    [Fact]
    public async Task Register_UnknownOrInactiveHousingUnit_IsRejectedByTheServiceAndNothingIsStored()
    {
        var inactive = await RegisterUnitAsync();
        await _database.Service.DeactivateAsync(inactive.Id);

        await Assert.ThrowsAsync<ArgumentException>(() => _database.BoardCases.RegisterAsync(Details(999)));
        await Assert.ThrowsAsync<ArgumentException>(() => _database.BoardCases.RegisterAsync(Details(inactive.Id)));

        Assert.Empty(await _database.BoardCases.GetAllAsync());
    }

    [Fact]
    public async Task Add_InvalidHousingUnitId_IsRejectedByTheDatabaseForeignKey()
    {
        var boardCase = new BoardCase(Details(housingUnitId: 999), _database.Clock.GetUtcNow().UtcDateTime);

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => _database.BoardCaseRepository.AddAsync(boardCase));

        var sqliteException = Assert.IsType<SqliteException>(exception.InnerException);
        Assert.Equal(787, sqliteException.SqliteExtendedErrorCode);
        Assert.Empty(await _database.BoardCases.GetAllAsync());
    }

    [Fact]
    public async Task Update_InvalidHousingUnitId_IsRejectedByTheDatabaseAndStoredCaseIsUnchanged()
    {
        var unit = await RegisterUnitAsync();
        var registered = await _database.BoardCases.RegisterAsync(Details(unit.Id));
        var stored = (await _database.BoardCaseRepository.GetByIdAsync(registered.Id))!;
        stored.UpdateDetails(stored.ToDetails() with { HousingUnitId = 999 }, _database.Clock.GetUtcNow().UtcDateTime);

        await Assert.ThrowsAsync<DbUpdateException>(() => _database.BoardCaseRepository.UpdateAsync(stored));

        Assert.Equal(unit.Id, (await _database.BoardCases.GetByIdAsync(registered.Id))!.HousingUnitId);
    }

    [Fact]
    public async Task HousingUnitLink_IsKeptWhenTheUnitIsDeactivatedAndStillReadable()
    {
        var unit = await RegisterUnitAsync();
        var registered = await _database.BoardCases.RegisterAsync(Details(unit.Id));

        await _database.Service.DeactivateAsync(unit.Id);
        await _database.RestartAsync();

        var found = await _database.BoardCases.GetByIdAsync(registered.Id);
        Assert.Equal(unit.Id, found!.HousingUnitId);
        Assert.Single(await _database.BoardCases.GetAllAsync());
    }

    // Deleting housing units

    [Fact]
    public async Task DeleteHousingUnit_WithBoardCase_IsBlockedAndNothingIsDeleted()
    {
        var unit = await RegisterUnitAsync();
        var registered = await _database.BoardCases.RegisterAsync(Details(unit.Id));

        var exception = await Assert.ThrowsAsync<HousingUnitInUseException>(() => _database.Service.DeleteAsync(unit.Id));
        await _database.RestartAsync();

        Assert.NotNull(await _database.Service.GetByIdAsync(unit.Id));
        Assert.Equal(unit.Id, (await _database.BoardCases.GetByIdAsync(registered.Id))!.HousingUnitId);
        var sqliteException = Assert.IsType<SqliteException>(exception.InnerException!.InnerException);
        Assert.Equal(1811, sqliteException.SqliteExtendedErrorCode);
    }

    [Fact]
    public async Task DeleteHousingUnit_AfterTheLinkIsRemoved_Succeeds()
    {
        var unit = await RegisterUnitAsync();
        var registered = await _database.BoardCases.RegisterAsync(Details(unit.Id));
        await _database.BoardCases.ChangeHousingUnitAsync(registered.Id, null);

        await _database.Service.DeleteAsync(unit.Id);
        await _database.RestartAsync();

        Assert.Null(await _database.Service.GetByIdAsync(unit.Id));
        var remaining = await _database.BoardCases.GetByIdAsync(registered.Id);
        Assert.Null(remaining!.HousingUnitId);
    }

    [Fact]
    public async Task DeleteHousingUnit_AfterTheBoardCaseIsDeleted_Succeeds()
    {
        var unit = await RegisterUnitAsync();
        var registered = await _database.BoardCases.RegisterAsync(Details(unit.Id));
        await _database.BoardCases.DeleteAsync(registered.Id);

        await _database.Service.DeleteAsync(unit.Id);

        Assert.Null(await _database.Service.GetByIdAsync(unit.Id));
    }

    [Fact]
    public async Task DeleteHousingUnit_WithoutBoardCases_StillWorksWhenOtherUnitsHaveCases()
    {
        var withCase = await RegisterUnitAsync("Storgata 1A");
        var withoutCase = await RegisterUnitAsync("Storgata 1B");
        await _database.BoardCases.RegisterAsync(Details(withCase.Id));

        await _database.Service.DeleteAsync(withoutCase.Id);

        Assert.Null(await _database.Service.GetByIdAsync(withoutCase.Id));
        Assert.NotNull(await _database.Service.GetByIdAsync(withCase.Id));
    }

    [Fact]
    public async Task DeleteBoardCase_DoesNotDeleteOrChangeTheHousingUnit()
    {
        var unit = await _database.Service.RegisterAsync(new HousingUnitDetails { Address = "Storgata 1A", OwnerName = "Test Eier" });
        var registered = await _database.BoardCases.RegisterAsync(Details(unit.Id));

        await _database.BoardCases.DeleteAsync(registered.Id);

        var found = await _database.Service.GetByIdAsync(unit.Id);
        Assert.Equal("Test Eier", found!.OwnerName);
    }
}
