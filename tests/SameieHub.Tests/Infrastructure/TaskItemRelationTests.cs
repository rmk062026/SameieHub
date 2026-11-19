using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SameieHub.Domain;

namespace SameieHub.Tests.Infrastructure;

public class TaskItemRelationTests : IAsyncLifetime
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

    private static TaskItemDetails Details(int? housingUnitId = null, int? boardCaseId = null) =>
        new() { Title = "Rense takrenner", HousingUnitId = housingUnitId, BoardCaseId = boardCaseId };

    private async Task<HousingUnit> RegisterUnitAsync(string address = "Storgata 1A")
    {
        return await _database.Service.RegisterAsync(address);
    }

    private async Task<BoardCase> RegisterBoardCaseAsync(int? housingUnitId = null)
    {
        return await _database.BoardCases.RegisterAsync(new BoardCaseDetails { Title = "Maling av porter", HousingUnitId = housingUnitId });
    }

    private TaskItem NewUnsavedTask(int? housingUnitId, int? boardCaseId)
    {
        return new TaskItem(Details(housingUnitId, boardCaseId), _database.Clock.GetUtcNow().UtcDateTime);
    }

    // Schema

    [Theory]
    [InlineData("HousingUnits")]
    [InlineData("BoardCases")]
    public async Task Schema_ForeignKeys_AreRestrict(string referencedTable)
    {
        var onDelete = await _database.QueryTextAsync(
            $"SELECT on_delete FROM pragma_foreign_key_list('TaskItems') WHERE \"table\" = '{referencedTable}'");

        Assert.Equal("RESTRICT", onDelete);
    }

    // The four combinations of links

    [Fact]
    public async Task Register_WithoutLinks_IsStored()
    {
        var task = await _database.Tasks.RegisterAsync(Details());

        await _database.RestartAsync();

        var found = await _database.Tasks.GetByIdAsync(task.Id);
        Assert.Null(found!.HousingUnitId);
        Assert.Null(found.BoardCaseId);
    }

    [Fact]
    public async Task Register_WithOnlyHousingUnit_IsStored()
    {
        var unit = await RegisterUnitAsync();

        var task = await _database.Tasks.RegisterAsync(Details(housingUnitId: unit.Id));
        await _database.RestartAsync();

        var found = await _database.Tasks.GetByIdAsync(task.Id);
        Assert.Equal(unit.Id, found!.HousingUnitId);
        Assert.Null(found.BoardCaseId);
    }

    [Fact]
    public async Task Register_WithOnlyBoardCase_IsStored()
    {
        var boardCase = await RegisterBoardCaseAsync();

        var task = await _database.Tasks.RegisterAsync(Details(boardCaseId: boardCase.Id));
        await _database.RestartAsync();

        var found = await _database.Tasks.GetByIdAsync(task.Id);
        Assert.Null(found!.HousingUnitId);
        Assert.Equal(boardCase.Id, found.BoardCaseId);
    }

    [Fact]
    public async Task Register_WithBothLinks_IsStoredAndTheBoardCaseMayConcernAnotherUnit()
    {
        var taskUnit = await RegisterUnitAsync("Storgata 1A");
        var caseUnit = await RegisterUnitAsync("Storgata 1B");
        var boardCase = await RegisterBoardCaseAsync(caseUnit.Id);

        var task = await _database.Tasks.RegisterAsync(Details(taskUnit.Id, boardCase.Id));
        await _database.RestartAsync();

        var found = await _database.Tasks.GetByIdAsync(task.Id);
        Assert.Equal(taskUnit.Id, found!.HousingUnitId);
        Assert.Equal(boardCase.Id, found.BoardCaseId);
    }

    [Fact]
    public async Task Register_BoardCaseThatConcernsTheWholeCooperative_CanBeLinked()
    {
        var unit = await RegisterUnitAsync();
        var boardCase = await RegisterBoardCaseAsync();

        var task = await _database.Tasks.RegisterAsync(Details(unit.Id, boardCase.Id));

        Assert.Equal(boardCase.Id, (await _database.Tasks.GetByIdAsync(task.Id))!.BoardCaseId);
    }

    [Fact]
    public async Task Register_BoardCaseWithAnyStatus_CanBeLinked()
    {
        var boardCase = await RegisterBoardCaseAsync();
        await _database.BoardCases.ChangeStatusAsync(boardCase.Id, BoardCaseStatus.Closed);

        var task = await _database.Tasks.RegisterAsync(Details(boardCaseId: boardCase.Id));

        Assert.Equal(boardCase.Id, task.BoardCaseId);
    }

    // Invalid references

    [Theory]
    [InlineData(999, null)]
    [InlineData(null, 999)]
    public async Task Add_InvalidReference_IsRejectedByTheDatabaseForeignKeyAndNothingIsStored(int? housingUnitId, int? boardCaseId)
    {
        var task = NewUnsavedTask(housingUnitId, boardCaseId);

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => _database.TaskRepository.AddAsync(task));

        var sqliteException = Assert.IsType<SqliteException>(exception.InnerException);
        Assert.Equal(787, sqliteException.SqliteExtendedErrorCode);
        Assert.Empty(await _database.Tasks.GetAllAsync());
    }

    [Fact]
    public async Task Update_InvalidReference_IsRejectedByTheDatabaseAndTheStoredTaskIsUnchanged()
    {
        var unit = await RegisterUnitAsync();
        var registered = await _database.Tasks.RegisterAsync(Details(housingUnitId: unit.Id));
        var stored = (await _database.TaskRepository.GetByIdAsync(registered.Id))!;
        stored.UpdateDetails(stored.ToDetails() with { BoardCaseId = 999 }, _database.Clock.GetUtcNow().UtcDateTime);

        await Assert.ThrowsAsync<DbUpdateException>(() => _database.TaskRepository.UpdateAsync(stored));

        var found = await _database.Tasks.GetByIdAsync(registered.Id);
        Assert.Equal(unit.Id, found!.HousingUnitId);
        Assert.Null(found.BoardCaseId);
    }

    [Fact]
    public async Task Register_UnknownOrInactiveHousingUnitAndUnknownBoardCase_AreRejectedByTheServiceFirst()
    {
        var inactive = await RegisterUnitAsync();
        await _database.Service.DeactivateAsync(inactive.Id);

        await Assert.ThrowsAsync<ArgumentException>(() => _database.Tasks.RegisterAsync(Details(housingUnitId: 999)));
        await Assert.ThrowsAsync<ArgumentException>(() => _database.Tasks.RegisterAsync(Details(housingUnitId: inactive.Id)));
        await Assert.ThrowsAsync<ArgumentException>(() => _database.Tasks.RegisterAsync(Details(boardCaseId: 999)));

        Assert.Empty(await _database.Tasks.GetAllAsync());
    }

    // Links to units that are deactivated later

    [Fact]
    public async Task HousingUnitLink_UnitDeactivatedLater_StillWorksForEditingStatusAndReading()
    {
        var unit = await RegisterUnitAsync();
        var task = await _database.Tasks.RegisterAsync(Details(housingUnitId: unit.Id));
        await _database.Service.DeactivateAsync(unit.Id);
        await _database.RestartAsync();

        await _database.Tasks.UpdateDetailsAsync(task.Id, Details(housingUnitId: unit.Id) with { Title = "Ny tittel" });
        await _database.Tasks.ChangeStatusAsync(task.Id, TaskItemStatus.Completed);
        await _database.RestartAsync();

        var found = await _database.Tasks.GetByIdAsync(task.Id);
        Assert.Equal("Ny tittel", found!.Title);
        Assert.Equal(TaskItemStatus.Completed, found.Status);
        Assert.Equal(unit.Id, found.HousingUnitId);
    }

    // Deletion locks, with real references

    [Fact]
    public async Task DeleteHousingUnit_ReferencedByATask_IsBlockedAndNothingIsDeleted()
    {
        var unit = await RegisterUnitAsync();
        var task = await _database.Tasks.RegisterAsync(Details(housingUnitId: unit.Id));

        var exception = await Assert.ThrowsAsync<HousingUnitInUseException>(() => _database.Service.DeleteAsync(unit.Id));
        await _database.RestartAsync();

        Assert.NotNull(await _database.Service.GetByIdAsync(unit.Id));
        Assert.Equal(unit.Id, (await _database.Tasks.GetByIdAsync(task.Id))!.HousingUnitId);
        var sqliteException = Assert.IsType<SqliteException>(exception.InnerException!.InnerException);
        Assert.Equal(1811, sqliteException.SqliteExtendedErrorCode);
    }

    [Fact]
    public async Task DeleteBoardCase_ReferencedByATask_IsBlockedAndNothingIsDeleted()
    {
        var boardCase = await RegisterBoardCaseAsync();
        var task = await _database.Tasks.RegisterAsync(Details(boardCaseId: boardCase.Id));

        var exception = await Assert.ThrowsAsync<BoardCaseInUseException>(() => _database.BoardCases.DeleteAsync(boardCase.Id));
        await _database.RestartAsync();

        Assert.NotNull(await _database.BoardCases.GetByIdAsync(boardCase.Id));
        Assert.Equal(boardCase.Id, (await _database.Tasks.GetByIdAsync(task.Id))!.BoardCaseId);
        var sqliteException = Assert.IsType<SqliteException>(exception.InnerException!.InnerException);
        Assert.Equal(1811, sqliteException.SqliteExtendedErrorCode);
    }

    [Fact]
    public async Task Delete_TaskLinkedToBoth_BlocksBothTheUnitAndTheBoardCase()
    {
        var unit = await RegisterUnitAsync();
        var boardCase = await RegisterBoardCaseAsync();
        await _database.Tasks.RegisterAsync(Details(unit.Id, boardCase.Id));

        await Assert.ThrowsAsync<HousingUnitInUseException>(() => _database.Service.DeleteAsync(unit.Id));
        await Assert.ThrowsAsync<BoardCaseInUseException>(() => _database.BoardCases.DeleteAsync(boardCase.Id));

        Assert.NotNull(await _database.Service.GetByIdAsync(unit.Id));
        Assert.NotNull(await _database.BoardCases.GetByIdAsync(boardCase.Id));
    }

    [Fact]
    public async Task DeleteHousingUnit_AfterTheLinkIsRemoved_Succeeds()
    {
        var unit = await RegisterUnitAsync();
        var task = await _database.Tasks.RegisterAsync(Details(housingUnitId: unit.Id));
        await _database.Tasks.ChangeHousingUnitAsync(task.Id, null);

        await _database.Service.DeleteAsync(unit.Id);
        await _database.RestartAsync();

        Assert.Null(await _database.Service.GetByIdAsync(unit.Id));
        Assert.NotNull(await _database.Tasks.GetByIdAsync(task.Id));
    }

    [Fact]
    public async Task DeleteBoardCase_AfterTheLinkIsRemoved_Succeeds()
    {
        var boardCase = await RegisterBoardCaseAsync();
        var task = await _database.Tasks.RegisterAsync(Details(boardCaseId: boardCase.Id));
        await _database.Tasks.ChangeBoardCaseAsync(task.Id, null);

        await _database.BoardCases.DeleteAsync(boardCase.Id);
        await _database.RestartAsync();

        Assert.Null(await _database.BoardCases.GetByIdAsync(boardCase.Id));
        Assert.NotNull(await _database.Tasks.GetByIdAsync(task.Id));
    }

    [Fact]
    public async Task Delete_AfterTheTaskIsDeleted_BothTheUnitAndTheBoardCaseCanBeDeleted()
    {
        var unit = await RegisterUnitAsync();
        var boardCase = await RegisterBoardCaseAsync();
        var task = await _database.Tasks.RegisterAsync(Details(unit.Id, boardCase.Id));
        await _database.Tasks.DeleteAsync(task.Id);

        await _database.Service.DeleteAsync(unit.Id);
        await _database.BoardCases.DeleteAsync(boardCase.Id);

        Assert.Null(await _database.Service.GetByIdAsync(unit.Id));
        Assert.Null(await _database.BoardCases.GetByIdAsync(boardCase.Id));
    }

    [Fact]
    public async Task Delete_UnitAndBoardCaseWithoutTasks_StillWorksWhenOthersHaveTasks()
    {
        var withTask = await RegisterUnitAsync("Storgata 1A");
        var withoutTask = await RegisterUnitAsync("Storgata 1B");
        var caseWithTask = await RegisterBoardCaseAsync();
        var caseWithoutTask = await RegisterBoardCaseAsync();
        await _database.Tasks.RegisterAsync(Details(withTask.Id, caseWithTask.Id));

        await _database.Service.DeleteAsync(withoutTask.Id);
        await _database.BoardCases.DeleteAsync(caseWithoutTask.Id);

        Assert.NotNull(await _database.Service.GetByIdAsync(withTask.Id));
        Assert.NotNull(await _database.BoardCases.GetByIdAsync(caseWithTask.Id));
    }

    [Fact]
    public async Task DeleteTask_NeverDeletesOrChangesTheUnitOrTheBoardCase()
    {
        var unit = await _database.Service.RegisterAsync(new HousingUnitDetails { Address = "Storgata 1A", OwnerName = "Test Eier" });
        var boardCase = await RegisterBoardCaseAsync(unit.Id);
        var task = await _database.Tasks.RegisterAsync(Details(unit.Id, boardCase.Id));

        await _database.Tasks.DeleteAsync(task.Id);
        await _database.RestartAsync();

        Assert.Equal("Test Eier", (await _database.Service.GetByIdAsync(unit.Id))!.OwnerName);
        Assert.Equal(unit.Id, (await _database.BoardCases.GetByIdAsync(boardCase.Id))!.HousingUnitId);
    }

    [Fact]
    public async Task DeleteHousingUnit_ReferencedOnlyByABoardCase_IsStillBlockedByTheBoardCaseRelation()
    {
        var unit = await RegisterUnitAsync();
        await RegisterBoardCaseAsync(unit.Id);

        await Assert.ThrowsAsync<HousingUnitInUseException>(() => _database.Service.DeleteAsync(unit.Id));
    }

    // Other database errors are not mistaken for reference errors

    [Fact]
    public async Task DeleteBoardCase_BlockedByACustomTrigger_IsNotReportedAsInUse()
    {
        var boardCase = await RegisterBoardCaseAsync();
        await _database.ExecuteSqlAsync(
            "CREATE TRIGGER block_board_case_delete BEFORE DELETE ON BoardCases " +
            "BEGIN SELECT RAISE(ABORT, 'Deleting is blocked by a custom rule'); END;");

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => _database.BoardCases.DeleteAsync(boardCase.Id));

        Assert.Contains("custom rule", exception.InnerException!.Message);
        Assert.NotNull(await _database.BoardCases.GetByIdAsync(boardCase.Id));
    }

    [Fact]
    public async Task DeleteHousingUnit_BlockedByACustomTrigger_IsNotReportedAsInUse()
    {
        var unit = await RegisterUnitAsync();
        await _database.ExecuteSqlAsync(
            "CREATE TRIGGER block_unit_delete BEFORE DELETE ON HousingUnits " +
            "BEGIN SELECT RAISE(ABORT, 'Deleting is blocked by a custom rule'); END;");

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => _database.Service.DeleteAsync(unit.Id));

        Assert.Contains("custom rule", exception.InnerException!.Message);
        Assert.NotNull(await _database.Service.GetByIdAsync(unit.Id));
    }
}
