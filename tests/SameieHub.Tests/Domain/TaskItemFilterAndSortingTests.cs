using SameieHub.Domain;

namespace SameieHub.Tests.Domain;

public class TaskItemFilterAndSortingTests
{
    private static readonly DateTime Start = new(2026, 11, 18, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = new(2026, 11, 18);

    private int _nextId = 1;

    private TaskItem NewTask(
        string title,
        int hoursAfterStart = 0,
        string? description = null,
        string? assignedTo = null,
        TaskItemCategory category = TaskItemCategory.General,
        TaskItemPriority priority = TaskItemPriority.Normal,
        DateOnly? dueDate = null,
        int? housingUnitId = null,
        int? boardCaseId = null,
        TaskItemStatus status = TaskItemStatus.NotStarted)
    {
        var createdAt = Start.AddHours(hoursAfterStart);
        var task = new TaskItem(new TaskItemDetails
        {
            Title = title,
            Description = description,
            AssignedTo = assignedTo,
            Category = category,
            Priority = priority,
            DueDate = dueDate,
            HousingUnitId = housingUnitId,
            BoardCaseId = boardCaseId
        }, createdAt);
        task.AssignId(_nextId++);
        task.ChangeStatus(status, createdAt);

        return task;
    }

    private static List<string> Titles(IEnumerable<TaskItem> tasks)
    {
        return tasks.Select(task => task.Title).ToList();
    }

    // Filter

    [Fact]
    public void Apply_NoCriteria_ReturnsEverythingIncludingCompleted()
    {
        var tasks = new[] { NewTask("Åpen"), NewTask("Ferdig", status: TaskItemStatus.Completed) };

        var result = TaskItemFilter.Apply(tasks, new TaskItemFilterCriteria());

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Apply_OpenOnly_HidesCompletedTasks()
    {
        var tasks = new[]
        {
            NewTask("Ikke startet"),
            NewTask("Pågår", status: TaskItemStatus.InProgress),
            NewTask("Ferdig", status: TaskItemStatus.Completed)
        };

        var result = TaskItemFilter.Apply(tasks, new TaskItemFilterCriteria { OpenOnly = true });

        Assert.Equal(["Ikke startet", "Pågår"], Titles(result));
    }

    [Fact]
    public void Apply_StatusCompleted_ShowsOnlyCompletedEvenWhenOpenOnlyIsSet()
    {
        var tasks = new[] { NewTask("Åpen"), NewTask("Ferdig", status: TaskItemStatus.Completed) };

        var result = TaskItemFilter.Apply(tasks, new TaskItemFilterCriteria { Status = TaskItemStatus.Completed, OpenOnly = true });

        Assert.Equal(["Ferdig"], Titles(result));
    }

    [Theory]
    [InlineData(TaskItemStatus.NotStarted, "Ikke startet")]
    [InlineData(TaskItemStatus.InProgress, "Pågår")]
    public void Apply_Status_ShowsOnlyThatStatus(TaskItemStatus status, string expectedTitle)
    {
        var tasks = new[]
        {
            NewTask("Ikke startet"),
            NewTask("Pågår", status: TaskItemStatus.InProgress),
            NewTask("Ferdig", status: TaskItemStatus.Completed)
        };

        var result = TaskItemFilter.Apply(tasks, new TaskItemFilterCriteria { Status = status });

        Assert.Equal([expectedTitle], Titles(result));
    }

    [Fact]
    public void Apply_Category_ShowsOnlyThatCategory()
    {
        var tasks = new[] { NewTask("Generell"), NewTask("Vedlikehold", category: TaskItemCategory.Maintenance) };

        var result = TaskItemFilter.Apply(tasks, new TaskItemFilterCriteria { Category = TaskItemCategory.Maintenance });

        Assert.Equal(["Vedlikehold"], Titles(result));
    }

    [Fact]
    public void Apply_Priority_ShowsOnlyThatPriority()
    {
        var tasks = new[] { NewTask("Normal"), NewTask("Høy", priority: TaskItemPriority.High) };

        var result = TaskItemFilter.Apply(tasks, new TaskItemFilterCriteria { Priority = TaskItemPriority.High });

        Assert.Equal(["Høy"], Titles(result));
    }

    [Fact]
    public void Apply_HousingUnit_ShowsOnlyTasksLinkedToThatUnit()
    {
        var tasks = new[] { NewTask("Enhet 1", housingUnitId: 1), NewTask("Enhet 2", housingUnitId: 2), NewTask("Ingen") };

        var result = TaskItemFilter.Apply(tasks, new TaskItemFilterCriteria { HousingUnitId = 1 });

        Assert.Equal(["Enhet 1"], Titles(result));
    }

    [Fact]
    public void Apply_BoardCase_ShowsOnlyTasksLinkedToThatCase()
    {
        var tasks = new[] { NewTask("Sak 5", boardCaseId: 5), NewTask("Sak 6", boardCaseId: 6), NewTask("Ingen") };

        var result = TaskItemFilter.Apply(tasks, new TaskItemFilterCriteria { BoardCaseId = 5 });

        Assert.Equal(["Sak 5"], Titles(result));
    }

    [Fact]
    public void Apply_OverdueOn_ShowsOnlyTasksThatAreOverdueThatDay()
    {
        var tasks = new[]
        {
            NewTask("Forfalt", dueDate: Today.AddDays(-1)),
            NewTask("Frist i dag", dueDate: Today),
            NewTask("Fremtid", dueDate: Today.AddDays(1)),
            NewTask("Uten frist"),
            NewTask("Fullført gammel frist", dueDate: Today.AddDays(-30), status: TaskItemStatus.Completed)
        };

        var result = TaskItemFilter.Apply(tasks, new TaskItemFilterCriteria { OverdueOn = Today });

        Assert.Equal(["Forfalt"], Titles(result));
    }

    [Fact]
    public void Apply_OverdueOnMidnightBoundary_UsesTheGivenDate()
    {
        var tasks = new[] { NewTask("Frist 18.", dueDate: new DateOnly(2026, 11, 18)) };

        Assert.Empty(TaskItemFilter.Apply(tasks, new TaskItemFilterCriteria { OverdueOn = new DateOnly(2026, 11, 18) }));
        Assert.Single(TaskItemFilter.Apply(tasks, new TaskItemFilterCriteria { OverdueOn = new DateOnly(2026, 11, 19) }));
    }

    [Theory]
    [InlineData("takrenne")]
    [InlineData("TAKRENNE")]
    [InlineData("  takrenne  ")]
    public void Apply_SearchText_MatchesTitleIgnoringCaseAndSpaces(string searchText)
    {
        var tasks = new[] { NewTask("Rense takrenner"), NewTask("Bytt pære") };

        var result = TaskItemFilter.Apply(tasks, new TaskItemFilterCriteria { SearchText = searchText });

        Assert.Equal(["Rense takrenner"], Titles(result));
    }

    [Fact]
    public void Apply_SearchText_MatchesDescriptionAndAssignedTo()
    {
        var tasks = new[]
        {
            NewTask("Oppgave A", description: "Gjelder fiktiv port"),
            NewTask("Oppgave B", assignedTo: "Test Person"),
            NewTask("Oppgave C")
        };

        Assert.Equal(["Oppgave A"], Titles(TaskItemFilter.Apply(tasks, new TaskItemFilterCriteria { SearchText = "fiktiv port" })));
        Assert.Equal(["Oppgave B"], Titles(TaskItemFilter.Apply(tasks, new TaskItemFilterCriteria { SearchText = "test person" })));
    }

    [Fact]
    public void Apply_SearchText_DoesNotMatchNotes()
    {
        var task = new TaskItem(new TaskItemDetails { Title = "Oppgave", Notes = "Hemmelig merknad" }, Start);
        task.AssignId(1);

        Assert.Empty(TaskItemFilter.Apply([task], new TaskItemFilterCriteria { SearchText = "hemmelig" }));
    }

    [Theory]
    [InlineData("2")]
    [InlineData("#2")]
    [InlineData(" #2 ")]
    public void Apply_SearchText_MatchesTaskNumberExactly(string searchText)
    {
        var tasks = new[] { NewTask("Første"), NewTask("Andre"), NewTask("Tredje") };

        var result = TaskItemFilter.Apply(tasks, new TaskItemFilterCriteria { SearchText = searchText });

        Assert.Equal(["Andre"], Titles(result));
    }

    [Fact]
    public void Apply_SearchText_NumberDoesNotMatchPartOfAnotherNumber()
    {
        var tasks = Enumerable.Range(1, 12).Select(_ => NewTask("Oppgave")).ToList();

        var result = TaskItemFilter.Apply(tasks, new TaskItemFilterCriteria { SearchText = "1" });

        Assert.Equal(1, Assert.Single(result).Id);
    }

    [Fact]
    public void Apply_BlankSearchText_ReturnsEverything()
    {
        var tasks = new[] { NewTask("A"), NewTask("B") };

        Assert.Equal(2, TaskItemFilter.Apply(tasks, new TaskItemFilterCriteria { SearchText = "   " }).Count);
    }

    [Fact]
    public void Apply_SeveralCriteria_AreCombined()
    {
        var tasks = new[]
        {
            NewTask("Treff", category: TaskItemCategory.Maintenance, priority: TaskItemPriority.High, housingUnitId: 1),
            NewTask("Feil prioritet", category: TaskItemCategory.Maintenance, housingUnitId: 1),
            NewTask("Feil enhet", category: TaskItemCategory.Maintenance, priority: TaskItemPriority.High, housingUnitId: 2)
        };

        var result = TaskItemFilter.Apply(tasks, new TaskItemFilterCriteria
        {
            Category = TaskItemCategory.Maintenance,
            Priority = TaskItemPriority.High,
            HousingUnitId = 1
        });

        Assert.Equal(["Treff"], Titles(result));
    }

    [Fact]
    public void Apply_DoesNotChangeTheInput()
    {
        var tasks = new List<TaskItem> { NewTask("A"), NewTask("B", status: TaskItemStatus.Completed) };

        TaskItemFilter.Apply(tasks, new TaskItemFilterCriteria { OpenOnly = true });

        Assert.Equal(2, tasks.Count);
    }

    // Sorting

    [Fact]
    public void Sort_DueDate_PutsNearestDueDateFirstAndOverdueBeforeFutureTasks()
    {
        var tasks = new[]
        {
            NewTask("Neste uke", dueDate: Today.AddDays(7)),
            NewTask("Forfalt", dueDate: Today.AddDays(-3)),
            NewTask("I morgen", dueDate: Today.AddDays(1))
        };

        var result = TaskItemSorting.Sort(tasks, TaskItemSortOrder.DueDate);

        Assert.Equal(["Forfalt", "I morgen", "Neste uke"], Titles(result));
    }

    [Fact]
    public void Sort_DueDate_PutsTasksWithoutDueDateLastNewestFirst()
    {
        var tasks = new[]
        {
            NewTask("Uten frist eldst"),
            NewTask("Med frist", hoursAfterStart: 1, dueDate: Today.AddDays(30)),
            NewTask("Uten frist nyest", hoursAfterStart: 2)
        };

        var result = TaskItemSorting.Sort(tasks, TaskItemSortOrder.DueDate);

        Assert.Equal(["Med frist", "Uten frist nyest", "Uten frist eldst"], Titles(result));
    }

    [Fact]
    public void Sort_DueDate_PutsCompletedTasksAfterOpenTasks()
    {
        var tasks = new[]
        {
            NewTask("Fullført gammel", dueDate: Today.AddDays(-30), status: TaskItemStatus.Completed),
            NewTask("Åpen langt frem", dueDate: Today.AddDays(90)),
            NewTask("Åpen uten frist")
        };

        var result = TaskItemSorting.Sort(tasks, TaskItemSortOrder.DueDate);

        Assert.Equal(["Åpen langt frem", "Åpen uten frist", "Fullført gammel"], Titles(result));
    }

    [Fact]
    public void Sort_DueDate_SameDueDateIsOrderedByIdForAStableResult()
    {
        var tasks = new[] { NewTask("Første", dueDate: Today), NewTask("Andre", dueDate: Today) };

        var result = TaskItemSorting.Sort(tasks.Reverse(), TaskItemSortOrder.DueDate);

        Assert.Equal(["Første", "Andre"], Titles(result));
    }

    [Fact]
    public void Sort_Newest_PutsLatestCreatedFirst()
    {
        var tasks = new[] { NewTask("Eldst"), NewTask("Nyest", hoursAfterStart: 2), NewTask("Midt", hoursAfterStart: 1) };

        Assert.Equal(["Nyest", "Midt", "Eldst"], Titles(TaskItemSorting.Sort(tasks, TaskItemSortOrder.Newest)));
    }

    [Fact]
    public void Sort_Oldest_PutsEarliestCreatedFirst()
    {
        var tasks = new[] { NewTask("Nyest", hoursAfterStart: 2), NewTask("Eldst"), NewTask("Midt", hoursAfterStart: 1) };

        Assert.Equal(["Eldst", "Midt", "Nyest"], Titles(TaskItemSorting.Sort(tasks, TaskItemSortOrder.Oldest)));
    }

    [Fact]
    public void Sort_Priority_PutsHighestFirstThenNearestDueDate()
    {
        var tasks = new[]
        {
            NewTask("Lav", priority: TaskItemPriority.Low),
            NewTask("Høy sent", priority: TaskItemPriority.High, dueDate: Today.AddDays(10)),
            NewTask("Høy tidlig", priority: TaskItemPriority.High, dueDate: Today.AddDays(1)),
            NewTask("Normal")
        };

        var result = TaskItemSorting.Sort(tasks, TaskItemSortOrder.Priority);

        Assert.Equal(["Høy tidlig", "Høy sent", "Normal", "Lav"], Titles(result));
    }

    [Fact]
    public void Sort_Title_UsesNorwegianAlphabetIgnoringCase()
    {
        var tasks = new[] { NewTask("Åpne port"), NewTask("Zebra"), NewTask("ære"), NewTask("anker") };

        var result = TaskItemSorting.Sort(tasks, TaskItemSortOrder.Title);

        Assert.Equal(["anker", "Zebra", "ære", "Åpne port"], Titles(result));
    }

    [Fact]
    public void Sort_ReturnsANewListAndKeepsTheInputOrder()
    {
        var tasks = new List<TaskItem> { NewTask("B"), NewTask("A", hoursAfterStart: 1) };

        var result = TaskItemSorting.Sort(tasks, TaskItemSortOrder.Title);

        Assert.Equal(["B", "A"], Titles(tasks));
        Assert.Equal(["A", "B"], Titles(result));
    }
}
