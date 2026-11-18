using SameieHub.Domain;

namespace SameieHub.Tests.Domain;

public class BoardCaseFilterAndSortingTests
{
    private static readonly DateTime Start = new(2026, 11, 18, 10, 0, 0, DateTimeKind.Utc);

    private int _nextId = 1;

    private BoardCase NewCase(
        string title,
        int hoursAfterStart = 0,
        string? description = null,
        BoardCasePriority priority = BoardCasePriority.Normal,
        int? housingUnitId = null,
        DateOnly? meetingDate = null)
    {
        var boardCase = new BoardCase(new BoardCaseDetails
        {
            Title = title,
            Description = description,
            Priority = priority,
            HousingUnitId = housingUnitId,
            MeetingDate = meetingDate
        }, Start.AddHours(hoursAfterStart));
        boardCase.AssignId(_nextId++);

        return boardCase;
    }

    // Filter

    [Fact]
    public void Apply_NoCriteria_ReturnsEverything()
    {
        var cases = new[] { NewCase("Sak 1"), NewCase("Sak 2") };

        var result = BoardCaseFilter.Apply(cases, new BoardCaseFilterCriteria());

        Assert.Equal(2, result.Count);
    }

    [Theory]
    [InlineData("maling")]
    [InlineData("  MALING  ")]
    [InlineData("fasade")]
    public void Apply_SearchText_FindsMatchInTitleOrDescriptionIgnoringCase(string searchText)
    {
        var match = NewCase("Maling av porter", description: "Gjelder fasaden");
        var other = NewCase("Vedtekter");

        var result = BoardCaseFilter.Apply([match, other], new BoardCaseFilterCriteria { SearchText = searchText });

        Assert.Same(match, Assert.Single(result));
    }

    [Theory]
    [InlineData("2")]
    [InlineData("#2")]
    public void Apply_NumericSearchText_MatchesTheCaseNumberExactly(string searchText)
    {
        var cases = Enumerable.Range(0, 12).Select(number => NewCase("Sak " + (char)('A' + number))).ToList();

        var result = BoardCaseFilter.Apply(cases, new BoardCaseFilterCriteria { SearchText = searchText });

        Assert.Equal(2, Assert.Single(result).Id);
    }

    [Fact]
    public void Apply_StatusFilter_ReturnsOnlyThatStatus()
    {
        var inProgress = NewCase("Sak 1");
        inProgress.ChangeStatus(BoardCaseStatus.InProgress, Start);
        var cases = new[] { inProgress, NewCase("Sak 2") };

        var result = BoardCaseFilter.Apply(cases, new BoardCaseFilterCriteria { Status = BoardCaseStatus.InProgress });

        Assert.Same(inProgress, Assert.Single(result));
    }

    [Fact]
    public void Apply_PriorityFilter_ReturnsOnlyThatPriority()
    {
        var high = NewCase("Sak 1", priority: BoardCasePriority.High);

        var result = BoardCaseFilter.Apply([high, NewCase("Sak 2")], new BoardCaseFilterCriteria { Priority = BoardCasePriority.High });

        Assert.Same(high, Assert.Single(result));
    }

    [Fact]
    public void Apply_HousingUnitFilter_ReturnsOnlyCasesForThatUnit()
    {
        var forUnit = NewCase("Sak 1", housingUnitId: 5);
        var cases = new[] { forUnit, NewCase("Sak 2", housingUnitId: 6), NewCase("Sak 3") };

        var result = BoardCaseFilter.Apply(cases, new BoardCaseFilterCriteria { HousingUnitId = 5 });

        Assert.Same(forUnit, Assert.Single(result));
    }

    [Fact]
    public void Apply_WholeCooperativeOnly_ReturnsOnlyCasesWithoutHousingUnit()
    {
        var wholeCooperative = NewCase("Sak 1");
        var cases = new[] { wholeCooperative, NewCase("Sak 2", housingUnitId: 6) };

        var result = BoardCaseFilter.Apply(cases, new BoardCaseFilterCriteria { WholeCooperativeOnly = true });

        Assert.Same(wholeCooperative, Assert.Single(result));
    }

    [Fact]
    public void Apply_SeveralCriteria_AreCombined()
    {
        var match = NewCase("Maling", priority: BoardCasePriority.High, housingUnitId: 5);
        var cases = new[] { match, NewCase("Maling", housingUnitId: 5), NewCase("Annet", priority: BoardCasePriority.High, housingUnitId: 5) };

        var result = BoardCaseFilter.Apply(cases, new BoardCaseFilterCriteria
        {
            SearchText = "maling",
            Priority = BoardCasePriority.High,
            HousingUnitId = 5
        });

        Assert.Same(match, Assert.Single(result));
    }

    [Fact]
    public void Apply_NoMatch_ReturnsEmptyList()
    {
        var result = BoardCaseFilter.Apply([NewCase("Sak 1")], new BoardCaseFilterCriteria { SearchText = "finnes-ikke" });

        Assert.Empty(result);
    }

    // Sorting

    [Fact]
    public void Sort_Newest_PutsLatestCreatedFirstAndBreaksTiesWithHighestId()
    {
        var oldest = NewCase("A", hoursAfterStart: 0);
        var middleOne = NewCase("B", hoursAfterStart: 1);
        var middleTwo = NewCase("C", hoursAfterStart: 1);
        var newest = NewCase("D", hoursAfterStart: 2);

        var sorted = BoardCaseSorting.Sort([oldest, middleOne, newest, middleTwo], BoardCaseSortOrder.Newest);

        Assert.Equal(["D", "C", "B", "A"], sorted.Select(boardCase => boardCase.Title));
    }

    [Fact]
    public void Sort_Oldest_PutsEarliestCreatedFirst()
    {
        var first = NewCase("A", hoursAfterStart: 0);
        var second = NewCase("B", hoursAfterStart: 1);

        var sorted = BoardCaseSorting.Sort([second, first], BoardCaseSortOrder.Oldest);

        Assert.Equal(["A", "B"], sorted.Select(boardCase => boardCase.Title));
    }

    [Fact]
    public void Sort_RecentlyUpdated_PutsLatestChangeFirst()
    {
        var first = NewCase("A", hoursAfterStart: 0);
        var second = NewCase("B", hoursAfterStart: 1);
        first.ChangeStatus(BoardCaseStatus.InProgress, Start.AddHours(5));

        var sorted = BoardCaseSorting.Sort([second, first], BoardCaseSortOrder.RecentlyUpdated);

        Assert.Equal(["A", "B"], sorted.Select(boardCase => boardCase.Title));
    }

    [Fact]
    public void Sort_MeetingDate_PutsEarliestMeetingFirstAndCasesWithoutMeetingLast()
    {
        var late = NewCase("Sent", meetingDate: new DateOnly(2026, 12, 20));
        var none = NewCase("Ingen");
        var early = NewCase("Tidlig", meetingDate: new DateOnly(2026, 12, 1));

        var sorted = BoardCaseSorting.Sort([late, none, early], BoardCaseSortOrder.MeetingDate);

        Assert.Equal(["Tidlig", "Sent", "Ingen"], sorted.Select(boardCase => boardCase.Title));
    }

    [Fact]
    public void Sort_Priority_PutsHighFirstAndNewestFirstWithinSamePriority()
    {
        var lowNew = NewCase("LavNy", hoursAfterStart: 3, priority: BoardCasePriority.Low);
        var highOld = NewCase("HøyGammel", hoursAfterStart: 0, priority: BoardCasePriority.High);
        var highNew = NewCase("HøyNy", hoursAfterStart: 2, priority: BoardCasePriority.High);
        var normal = NewCase("Normal", hoursAfterStart: 1);

        var sorted = BoardCaseSorting.Sort([lowNew, highOld, normal, highNew], BoardCaseSortOrder.Priority);

        Assert.Equal(["HøyNy", "HøyGammel", "Normal", "LavNy"], sorted.Select(boardCase => boardCase.Title));
    }

    [Fact]
    public void Sort_Title_IsAlphabeticalIgnoringCase()
    {
        var sorted = BoardCaseSorting.Sort([NewCase("vedtekter"), NewCase("Brannvern"), NewCase("Maling")], BoardCaseSortOrder.Title);

        Assert.Equal(["Brannvern", "Maling", "vedtekter"], sorted.Select(boardCase => boardCase.Title));
    }

    [Fact]
    public void Sort_DoesNotChangeTheInputCollection()
    {
        var cases = new List<BoardCase> { NewCase("A", hoursAfterStart: 0), NewCase("B", hoursAfterStart: 1) };

        BoardCaseSorting.Sort(cases, BoardCaseSortOrder.Newest);

        Assert.Equal(["A", "B"], cases.Select(boardCase => boardCase.Title));
    }
}
