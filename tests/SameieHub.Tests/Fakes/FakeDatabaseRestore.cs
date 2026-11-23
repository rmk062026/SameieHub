using SameieHub.Infrastructure.Restore;

namespace SameieHub.Tests.Fakes;

/// <summary>A restore engine that only remembers a last outcome. Used where a page just shows the result of an earlier restore.</summary>
public sealed class FakeDatabaseRestore : IDatabaseRestore
{
    public RestoreOutcome? LastOutcome { get; set; }

    public PendingRestore? Pending { get; set; }

    public Task<RestorePreparation> PrepareAsync(string backupFilePath, CancellationToken cancellationToken = default, string? displayName = null) =>
        throw new NotSupportedException("This fake only shows results.");

    public RestoreOutcome ApplyPendingRestore() => throw new NotSupportedException("This fake only shows results.");

    public PendingRestore? GetPending() => Pending;

    public void CancelPending() => Pending = null;

    public RestoreOutcome? GetLastOutcome() => LastOutcome;

    public void ClearLastOutcome() => LastOutcome = null;
}
