using SameieHub.Domain;

namespace SameieHub.Tests.Domain;

public class BoardCaseTests
{
    private static readonly DateTime CreatedAt = new(2026, 11, 18, 10, 30, 0, DateTimeKind.Utc);
    private static readonly DateTime Later = CreatedAt.AddHours(1);

    private static BoardCaseDetails FullDetails() => new()
    {
        Title = "Maling av fellesarealer",
        Description = "Fiktiv beskrivelse.",
        Priority = BoardCasePriority.High,
        MeetingDate = new DateOnly(2026, 12, 3),
        HousingUnitId = 7,
        Notes = "Fiktive merknader."
    };

    private static BoardCase NewCase() => new(FullDetails(), CreatedAt);

    // Creation

    [Fact]
    public void Constructor_OnlyTitle_UsesDefaultsAndNoOptionalValues()
    {
        var boardCase = new BoardCase(new BoardCaseDetails { Title = "Vedtekter" }, CreatedAt);

        Assert.Equal("Vedtekter", boardCase.Title);
        Assert.Equal(BoardCaseStatus.New, boardCase.Status);
        Assert.Equal(BoardCasePriority.Normal, boardCase.Priority);
        Assert.Equal(CreatedAt, boardCase.CreatedAt);
        Assert.Equal(0, boardCase.Id);
        Assert.Null(boardCase.Description);
        Assert.Null(boardCase.MeetingDate);
        Assert.Null(boardCase.Decision);
        Assert.Null(boardCase.HousingUnitId);
        Assert.Null(boardCase.Notes);
    }

    [Fact]
    public void Constructor_AllDetails_SetsAllValues()
    {
        var boardCase = NewCase();

        AssertHasDetails(boardCase, FullDetails());
        Assert.Equal(BoardCaseStatus.New, boardCase.Status);
        Assert.Equal(CreatedAt, boardCase.CreatedAt);
    }

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void Constructor_CreatedAtNotUtc_Throws(DateTimeKind kind)
    {
        var notUtc = new DateTime(2026, 11, 18, 10, 30, 0, kind);

        var exception = Assert.Throws<ArgumentException>(() => new BoardCase(FullDetails(), notUtc));

        Assert.Equal("createdAt", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_MissingTitle_Throws(string? title)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new BoardCase(FullDetails() with { Title = title! }, CreatedAt));

        Assert.Equal("Title", exception.ParamName);
    }

    [Fact]
    public void Constructor_InvalidOptionalValue_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new BoardCase(FullDetails() with { Description = new string('d', BoardCase.MaxDescriptionLength + 1) }, CreatedAt));
    }

    [Fact]
    public void Constructor_NoHousingUnit_MeansWholeCooperative()
    {
        var boardCase = new BoardCase(FullDetails() with { HousingUnitId = null }, CreatedAt);

        Assert.Null(boardCase.HousingUnitId);
    }

    // Normalization

    [Fact]
    public void Constructor_TextWithSurroundingSpaces_IsTrimmed()
    {
        var boardCase = new BoardCase(new BoardCaseDetails
        {
            Title = "  Vedtekter  ",
            Description = " Beskrivelse ",
            Notes = "  Merknad  "
        }, CreatedAt);

        Assert.Equal("Vedtekter", boardCase.Title);
        Assert.Equal("Beskrivelse", boardCase.Description);
        Assert.Equal("Merknad", boardCase.Notes);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void UpdateDetails_EmptyOptionalText_BecomesNull(string? empty)
    {
        var boardCase = NewCase();

        boardCase.UpdateDetails(FullDetails() with { Description = empty, Notes = empty }, Later);

        Assert.Null(boardCase.Description);
        Assert.Null(boardCase.Notes);
    }

    // Limits

    [Fact]
    public void UpdateDetails_ValuesAtTheLimits_AreAccepted()
    {
        var boardCase = NewCase();

        boardCase.UpdateDetails(new BoardCaseDetails
        {
            Title = new string('t', BoardCase.MaxTitleLength),
            Description = new string('d', BoardCase.MaxDescriptionLength),
            Notes = new string('n', BoardCase.MaxNotesLength),
            MeetingDate = BoardCase.MinMeetingDate,
            HousingUnitId = 1
        }, Later);
        boardCase.UpdateDetails(FullDetails() with { MeetingDate = BoardCase.MaxMeetingDate }, Later);

        Assert.Equal(BoardCase.MaxMeetingDate, boardCase.MeetingDate);
    }

    [Fact]
    public void UpdateDetails_TooLongText_ThrowsAndChangesNothing()
    {
        AssertRejectedAndUnchanged(FullDetails() with { Title = new string('t', BoardCase.MaxTitleLength + 1) }, "Title");
        AssertRejectedAndUnchanged(FullDetails() with { Description = new string('d', BoardCase.MaxDescriptionLength + 1) }, "Description");
        AssertRejectedAndUnchanged(FullDetails() with { Notes = new string('n', BoardCase.MaxNotesLength + 1) }, "Notes");
    }

    // Update

    [Fact]
    public void UpdateDetails_NewValues_ReplacesAllDetailsAndKeepsStatusAndCreatedAt()
    {
        var boardCase = NewCase();
        boardCase.RegisterDecision("Fiktivt vedtak.", Later);
        boardCase.ChangeStatus(BoardCaseStatus.Decided, Later);
        var changed = new BoardCaseDetails
        {
            Title = "Ny tittel",
            Priority = BoardCasePriority.Low,
            MeetingDate = new DateOnly(2027, 1, 15)
        };

        boardCase.UpdateDetails(changed, Later);

        AssertHasDetails(boardCase, changed);
        Assert.Null(boardCase.Description);
        Assert.Null(boardCase.HousingUnitId);
        Assert.Equal(BoardCaseStatus.Decided, boardCase.Status);
        Assert.Equal("Fiktivt vedtak.", boardCase.Decision);
        Assert.Equal(CreatedAt, boardCase.CreatedAt);
    }

    [Fact]
    public void UpdateDetails_BlankTitle_ThrowsAndChangesNothing()
    {
        AssertRejectedAndUnchanged(FullDetails() with { Title = "   ", Notes = "Skal ikke lagres" }, "Title");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    public void UpdateDetails_UndefinedPriority_ThrowsAndChangesNothing(int priority)
    {
        AssertRejectedAndUnchanged(FullDetails() with { Title = "Ny tittel", Priority = (BoardCasePriority)priority }, "Priority");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void UpdateDetails_InvalidHousingUnitId_ThrowsAndChangesNothing(int housingUnitId)
    {
        AssertRejectedAndUnchanged(FullDetails() with { Title = "Ny tittel", HousingUnitId = housingUnitId }, "HousingUnitId");
    }

    [Fact]
    public void UpdateDetails_MeetingDateOutsideAllowedRange_ThrowsAndChangesNothing()
    {
        AssertRejectedAndUnchanged(FullDetails() with { MeetingDate = BoardCase.MinMeetingDate.AddDays(-1) }, "MeetingDate");
        AssertRejectedAndUnchanged(FullDetails() with { MeetingDate = BoardCase.MaxMeetingDate.AddDays(1) }, "MeetingDate");
    }

    [Fact]
    public void UpdateDetails_RemovingMeetingDateAndHousingUnit_Works()
    {
        var boardCase = NewCase();

        boardCase.UpdateDetails(FullDetails() with { MeetingDate = null, HousingUnitId = null }, Later);

        Assert.Null(boardCase.MeetingDate);
        Assert.Null(boardCase.HousingUnitId);
    }

    // Status

    [Fact]
    public void ChangeStatus_ToInProgress_ChangesStatus()
    {
        var boardCase = NewCase();

        boardCase.ChangeStatus(BoardCaseStatus.InProgress, Later);

        Assert.Equal(BoardCaseStatus.InProgress, boardCase.Status);
    }

    [Fact]
    public void ChangeStatus_ToDecidedWithoutDecision_ThrowsAndKeepsStatus()
    {
        var boardCase = NewCase();

        Assert.Throws<InvalidOperationException>(() => boardCase.ChangeStatus(BoardCaseStatus.Decided, Later));

        Assert.Equal(BoardCaseStatus.New, boardCase.Status);
    }

    [Fact]
    public void ChangeStatus_ToDecidedWithDecision_BecomesDecidedButNotClosed()
    {
        var boardCase = NewCase();
        boardCase.RegisterDecision("Fiktivt vedtak.", Later);

        boardCase.ChangeStatus(BoardCaseStatus.Decided, Later);

        Assert.Equal(BoardCaseStatus.Decided, boardCase.Status);
    }

    [Theory]
    [InlineData(BoardCaseStatus.New)]
    [InlineData(BoardCaseStatus.InProgress)]
    [InlineData(BoardCaseStatus.Decided)]
    [InlineData(BoardCaseStatus.Closed)]
    public void ChangeStatus_AnyStatusToAnyOther_IsAllowedWhenCaseHasDecision(BoardCaseStatus from)
    {
        foreach (var to in Enum.GetValues<BoardCaseStatus>())
        {
            var boardCase = NewCase();
            boardCase.RegisterDecision("Fiktivt vedtak.", Later);
            boardCase.ChangeStatus(from, Later);

            boardCase.ChangeStatus(to, Later);

            Assert.Equal(to, boardCase.Status);
        }
    }

    [Fact]
    public void ChangeStatus_ReopeningClosedCase_IsAllowedAndKeepsDecision()
    {
        var boardCase = NewCase();
        boardCase.RegisterDecision("Fiktivt vedtak.", Later);
        boardCase.ChangeStatus(BoardCaseStatus.Closed, Later);

        boardCase.ChangeStatus(BoardCaseStatus.InProgress, Later);

        Assert.Equal(BoardCaseStatus.InProgress, boardCase.Status);
        Assert.Equal("Fiktivt vedtak.", boardCase.Decision);
    }

    [Fact]
    public void ChangeStatus_ClosingCaseWithoutDecision_IsAllowed()
    {
        var boardCase = NewCase();

        boardCase.ChangeStatus(BoardCaseStatus.Closed, Later);

        Assert.Equal(BoardCaseStatus.Closed, boardCase.Status);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    public void ChangeStatus_UndefinedStatus_ThrowsAndKeepsStatus(int status)
    {
        var boardCase = NewCase();

        Assert.Throws<ArgumentOutOfRangeException>(() => boardCase.ChangeStatus((BoardCaseStatus)status, Later));

        Assert.Equal(BoardCaseStatus.New, boardCase.Status);
    }

    // Decision

    [Fact]
    public void RegisterDecision_ValidText_IsTrimmedAndDoesNotChangeStatus()
    {
        var boardCase = NewCase();

        boardCase.RegisterDecision("  Fiktivt vedtak.  ", Later);

        Assert.Equal("Fiktivt vedtak.", boardCase.Decision);
        Assert.Equal(BoardCaseStatus.New, boardCase.Status);
    }

    [Fact]
    public void RegisterDecision_ExistingDecision_IsCorrected()
    {
        var boardCase = NewCase();
        boardCase.RegisterDecision("Feil vedtak.", Later);
        boardCase.ChangeStatus(BoardCaseStatus.Decided, Later);

        boardCase.RegisterDecision("Riktig vedtak.", Later);

        Assert.Equal("Riktig vedtak.", boardCase.Decision);
        Assert.Equal(BoardCaseStatus.Decided, boardCase.Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RegisterDecision_Blank_ThrowsAndKeepsExistingDecision(string? blank)
    {
        var boardCase = NewCase();
        boardCase.RegisterDecision("Fiktivt vedtak.", Later);

        var exception = Assert.Throws<ArgumentException>(() => boardCase.RegisterDecision(blank!, Later));

        Assert.Equal("Decision", exception.ParamName);
        Assert.Equal("Fiktivt vedtak.", boardCase.Decision);
    }

    [Fact]
    public void RegisterDecision_TextAtAndOverTheLimit_IsAcceptedAndRejected()
    {
        var boardCase = NewCase();
        boardCase.RegisterDecision(new string('v', BoardCase.MaxDecisionLength), Later);

        Assert.Throws<ArgumentException>(() => boardCase.RegisterDecision(new string('x', BoardCase.MaxDecisionLength + 1), Later));

        Assert.Equal(new string('v', BoardCase.MaxDecisionLength), boardCase.Decision);
    }

    [Fact]
    public void ClearDecision_CaseNotDecided_RemovesDecision()
    {
        var boardCase = NewCase();
        boardCase.RegisterDecision("Registrert ved en feil.", Later);

        boardCase.ClearDecision(Later);

        Assert.Null(boardCase.Decision);
    }

    [Fact]
    public void ClearDecision_NoDecision_ChangesNothing()
    {
        var boardCase = NewCase();

        boardCase.ClearDecision(Later);

        Assert.Null(boardCase.Decision);
    }

    [Fact]
    public void ClearDecision_CaseIsDecided_ThrowsAndKeepsDecision()
    {
        var boardCase = NewCase();
        boardCase.RegisterDecision("Fiktivt vedtak.", Later);
        boardCase.ChangeStatus(BoardCaseStatus.Decided, Later);

        Assert.Throws<InvalidOperationException>(() => boardCase.ClearDecision(Later));

        Assert.Equal("Fiktivt vedtak.", boardCase.Decision);
    }

    [Fact]
    public void ClearDecision_AfterChangingStatusBackFromDecided_Works()
    {
        var boardCase = NewCase();
        boardCase.RegisterDecision("Fiktivt vedtak.", Later);
        boardCase.ChangeStatus(BoardCaseStatus.Decided, Later);
        boardCase.ChangeStatus(BoardCaseStatus.InProgress, Later);

        boardCase.ClearDecision(Later);

        Assert.Null(boardCase.Decision);
    }

    // Id

    [Fact]
    public void AssignId_PositiveId_SetsId()
    {
        var boardCase = NewCase();

        boardCase.AssignId(5);

        Assert.Equal(5, boardCase.Id);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AssignId_ZeroOrNegative_Throws(int id)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NewCase().AssignId(id));
    }

    [Fact]
    public void AssignId_AlreadyAssigned_ThrowsAndKeepsOldId()
    {
        var boardCase = NewCase();
        boardCase.AssignId(5);

        Assert.Throws<InvalidOperationException>(() => boardCase.AssignId(6));

        Assert.Equal(5, boardCase.Id);
    }

    // UpdatedAt

    [Fact]
    public void Constructor_SetsUpdatedAtToCreatedAt()
    {
        Assert.Equal(CreatedAt, NewCase().UpdatedAt);
    }

    [Fact]
    public void UpdateDetails_RealChange_SetsUpdatedAtAndKeepsCreatedAt()
    {
        var boardCase = NewCase();

        boardCase.UpdateDetails(FullDetails() with { Title = "Ny tittel" }, Later);

        Assert.Equal(Later, boardCase.UpdatedAt);
        Assert.Equal(CreatedAt, boardCase.CreatedAt);
    }

    [Fact]
    public void UpdateDetails_SameValues_DoesNotChangeUpdatedAt()
    {
        var boardCase = NewCase();

        boardCase.UpdateDetails(FullDetails(), Later);

        Assert.Equal(CreatedAt, boardCase.UpdatedAt);
    }

    [Fact]
    public void UpdateDetails_SameValuesWithExtraSpaces_DoesNotChangeUpdatedAt()
    {
        var boardCase = NewCase();

        boardCase.UpdateDetails(FullDetails() with { Title = "  Maling av fellesarealer  " }, Later);

        Assert.Equal(CreatedAt, boardCase.UpdatedAt);
    }

    [Fact]
    public void UpdateDetails_InvalidValue_DoesNotChangeUpdatedAt()
    {
        var boardCase = NewCase();

        Assert.Throws<ArgumentException>(() => boardCase.UpdateDetails(FullDetails() with { Title = " " }, Later));

        Assert.Equal(CreatedAt, boardCase.UpdatedAt);
    }

    [Fact]
    public void ChangeStatus_RealChange_SetsUpdatedAt()
    {
        var boardCase = NewCase();

        boardCase.ChangeStatus(BoardCaseStatus.InProgress, Later);

        Assert.Equal(Later, boardCase.UpdatedAt);
    }

    [Fact]
    public void ChangeStatus_SameStatus_DoesNotChangeUpdatedAt()
    {
        var boardCase = NewCase();

        boardCase.ChangeStatus(BoardCaseStatus.New, Later);

        Assert.Equal(CreatedAt, boardCase.UpdatedAt);
    }

    [Fact]
    public void ChangeStatus_Rejected_DoesNotChangeUpdatedAt()
    {
        var boardCase = NewCase();

        Assert.Throws<InvalidOperationException>(() => boardCase.ChangeStatus(BoardCaseStatus.Decided, Later));

        Assert.Equal(CreatedAt, boardCase.UpdatedAt);
    }

    [Fact]
    public void RegisterDecision_NewText_SetsUpdatedAt()
    {
        var boardCase = NewCase();

        boardCase.RegisterDecision("Fiktivt vedtak.", Later);

        Assert.Equal(Later, boardCase.UpdatedAt);
    }

    [Fact]
    public void RegisterDecision_SameText_DoesNotChangeUpdatedAt()
    {
        var boardCase = NewCase();
        boardCase.RegisterDecision("Fiktivt vedtak.", Later);

        boardCase.RegisterDecision("  Fiktivt vedtak.  ", Later.AddHours(1));

        Assert.Equal(Later, boardCase.UpdatedAt);
    }

    [Fact]
    public void RegisterDecision_Blank_DoesNotChangeUpdatedAt()
    {
        var boardCase = NewCase();

        Assert.Throws<ArgumentException>(() => boardCase.RegisterDecision("  ", Later));

        Assert.Equal(CreatedAt, boardCase.UpdatedAt);
    }

    [Fact]
    public void ClearDecision_ExistingDecision_SetsUpdatedAt()
    {
        var boardCase = NewCase();
        boardCase.RegisterDecision("Fiktivt vedtak.", Later);

        boardCase.ClearDecision(Later.AddHours(1));

        Assert.Equal(Later.AddHours(1), boardCase.UpdatedAt);
    }

    [Fact]
    public void ClearDecision_NoDecision_DoesNotChangeUpdatedAt()
    {
        var boardCase = NewCase();

        boardCase.ClearDecision(Later);

        Assert.Equal(CreatedAt, boardCase.UpdatedAt);
    }

    [Fact]
    public void ClearDecision_CaseIsDecided_DoesNotChangeUpdatedAt()
    {
        var boardCase = NewCase();
        boardCase.RegisterDecision("Fiktivt vedtak.", Later);
        boardCase.ChangeStatus(BoardCaseStatus.Decided, Later.AddHours(1));

        Assert.Throws<InvalidOperationException>(() => boardCase.ClearDecision(Later.AddHours(2)));

        Assert.Equal(Later.AddHours(1), boardCase.UpdatedAt);
    }

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void Changes_TimeNotUtc_Throws(DateTimeKind kind)
    {
        var boardCase = NewCase();
        var notUtc = new DateTime(2026, 11, 19, 10, 0, 0, kind);

        Assert.Equal("now", Assert.Throws<ArgumentException>(() => boardCase.UpdateDetails(FullDetails() with { Title = "Ny" }, notUtc)).ParamName);
        Assert.Equal("now", Assert.Throws<ArgumentException>(() => boardCase.ChangeStatus(BoardCaseStatus.InProgress, notUtc)).ParamName);
        Assert.Equal("now", Assert.Throws<ArgumentException>(() => boardCase.RegisterDecision("Vedtak", notUtc)).ParamName);
        Assert.Equal("now", Assert.Throws<ArgumentException>(() => boardCase.ClearDecision(notUtc)).ParamName);
        Assert.Equal(CreatedAt, boardCase.UpdatedAt);
    }

    // ToDetails

    [Fact]
    public void ToDetails_ReturnsCurrentValues()
    {
        var boardCase = NewCase();

        Assert.Equal(FullDetails(), boardCase.ToDetails());
    }

    [Fact]
    public void ToDetails_UsedForUpdate_ChangesNothing()
    {
        var boardCase = NewCase();

        boardCase.UpdateDetails(boardCase.ToDetails(), Later);

        Assert.Equal(CreatedAt, boardCase.UpdatedAt);
    }

    private static void AssertRejectedAndUnchanged(BoardCaseDetails invalid, string expectedField)
    {
        var boardCase = NewCase();

        var exception = Assert.ThrowsAny<ArgumentException>(() => boardCase.UpdateDetails(invalid, Later));

        Assert.Equal(expectedField, exception.ParamName);
        AssertHasDetails(boardCase, FullDetails());
        Assert.Equal(CreatedAt, boardCase.UpdatedAt);
    }

    private static void AssertHasDetails(BoardCase boardCase, BoardCaseDetails expected)
    {
        Assert.Equal(expected.Title, boardCase.Title);
        Assert.Equal(expected.Description, boardCase.Description);
        Assert.Equal(expected.Priority, boardCase.Priority);
        Assert.Equal(expected.MeetingDate, boardCase.MeetingDate);
        Assert.Equal(expected.HousingUnitId, boardCase.HousingUnitId);
        Assert.Equal(expected.Notes, boardCase.Notes);
    }
}
