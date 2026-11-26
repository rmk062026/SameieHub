using SameieHub.Infrastructure.Persistence;
using SameieHub.Infrastructure.Restore;

namespace SameieHub.Web.Hosting;

internal enum StartupFailureReason
{
    AlreadyRunning,
    FolderNotWritable,
    RestoreUnresolved,
    DatabaseNewerThanProgram,
    DatabaseNotSameieHub,
    DatabaseDamaged,
    BackupBeforeUpgradeFailed,
    MigrationFailed,
    DemoDatabaseNotAllowed,
    ServerCouldNotStart
}

/// <param name="Reason">What stopped the start. Only this is logged, never the message, which can contain a path.</param>
/// <param name="Message">What the person is told, in Norwegian.</param>
internal sealed record StartupFailure(StartupFailureReason Reason, string Message);

/// <summary>Either the lock the program must hold while it runs, or the reason it cannot start.</summary>
internal sealed class StartupResult : IDisposable
{
    public StartupResult(InstanceLock? instanceLock, StartupFailure? failure)
    {
        Lock = instanceLock;
        Failure = failure;
    }

    public InstanceLock? Lock { get; }

    public StartupFailure? Failure { get; }

    public void Dispose()
    {
        Lock?.Dispose();
    }
}

/// <summary>
/// The steps before the web server starts, in the order that protects the data: take the instance lock, carry out a
/// prepared restore (before any database connection is opened), then check the database version, back up and migrate.
/// </summary>
internal static class WindowsStartup
{
    public static async Task<StartupResult> PrepareAsync(
        string databasePath,
        IDatabaseRestore restore,
        DatabaseUpgrader upgrader,
        ILogger logger)
    {
        InstanceLock? instanceLock;
        try
        {
            instanceLock = InstanceLock.TryAcquire(InstanceLock.PathFor(databasePath));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Failed(StartupFailureReason.FolderNotWritable, logger);
        }

        if (instanceLock == null)
        {
            return Failed(StartupFailureReason.AlreadyRunning, logger);
        }

        var restoreOutcome = restore.ApplyPendingRestore();
        if (restoreOutcome.Kind != RestoreOutcomeKind.NothingPending)
        {
            logger.LogWarning("Database restore at startup: {Result}.", restoreOutcome.Kind);
        }

        if (restoreOutcome.Kind == RestoreOutcomeKind.Unresolved)
        {
            instanceLock.Dispose();

            return Failed(StartupFailureReason.RestoreUnresolved, logger);
        }

        try
        {
            await upgrader.UpgradeAsync();
        }
        catch (DatabaseUpgradeException exception)
        {
            instanceLock.Dispose();

            return new StartupResult(null, Report(StartupMessages.For(exception), logger));
        }

        return new StartupResult(instanceLock, null);
    }

    private static StartupResult Failed(StartupFailureReason reason, ILogger logger)
    {
        return new StartupResult(null, Report(StartupMessages.For(reason), logger));
    }

    private static StartupFailure Report(StartupFailure failure, ILogger logger)
    {
        logger.LogError("SameieHub could not start: {Reason}.", failure.Reason);

        return failure;
    }
}

/// <summary>The Norwegian messages. They are written for a person who is not a developer: what happened, what is safe, and what to do.</summary>
internal static class StartupMessages
{
    public static StartupFailure For(StartupFailureReason reason)
    {
        return new StartupFailure(reason, reason switch
        {
            StartupFailureReason.AlreadyRunning =>
                "SameieHub kjører allerede. Se etter vinduet «SameieHub» i oppgavelinjen. Lukk det vinduet hvis du vil starte på nytt.",
            StartupFailureReason.FolderNotWritable =>
                "SameieHub fikk ikke skrive til mappen der dataene ligger. Kontroller at mappen finnes og at du har tillatelse til å skrive til den.",
            StartupFailureReason.RestoreUnresolved =>
                "SameieHub kunne ikke starte, fordi en gjenoppretting ble avbrutt og ikke kunne fullføres trygt. Ingenting er slettet, og ingen ny database er laget. "
                + "Den gamle og den klargjorte databasen ligger i mappen «restore» ved siden av databasefilen. Se veiledningen «Hvis gjenopprettingen står uavklart» i dokumentasjonen før du starter på nytt.",
            StartupFailureReason.DatabaseNewerThanProgram =>
                "Databasen er laget av en nyere versjon av SameieHub enn denne. Programmet har ikke åpnet databasen og ikke endret den. Installer den nyeste versjonen av SameieHub.",
            StartupFailureReason.DatabaseNotSameieHub =>
                "Filen som skal være SameieHub-databasen, er ikke en SameieHub-database. Programmet har ikke endret den. Kontroller at riktig database er valgt.",
            StartupFailureReason.DatabaseDamaged =>
                "Databasefilen er skadet eller kan ikke leses. Programmet har ikke endret den. Gjenopprett fra en sikkerhetskopi, eller få hjelp.",
            StartupFailureReason.BackupBeforeUpgradeFailed =>
                "SameieHub kunne ikke lage en sikkerhetskopi før databasen skulle oppgraderes, og stoppet derfor uten å oppgradere. Kontroller at det er ledig plass og at mappen med databasen kan skrives til.",
            StartupFailureReason.MigrationFailed =>
                "Databasen kunne ikke oppgraderes. Programmet er stoppet.",
            StartupFailureReason.DemoDatabaseNotAllowed =>
                "Demodatabasen kan ikke brukes i Windows-modus. Start demoen med Start-demo.cmd.",
            StartupFailureReason.ServerCouldNotStart =>
                "SameieHub kunne ikke starte den lokale serveren. Start programmet på nytt. Hvis det skjer igjen, se loggfilen i mappen «logs».",
            _ => "SameieHub kunne ikke starte."
        });
    }

    public static StartupFailure For(DatabaseUpgradeException exception)
    {
        switch (exception.Reason)
        {
            case DatabaseUpgradeFailure.NewerThanProgram:
                return For(StartupFailureReason.DatabaseNewerThanProgram);
            case DatabaseUpgradeFailure.NotSameieHubDatabase:
                return For(StartupFailureReason.DatabaseNotSameieHub);
            case DatabaseUpgradeFailure.DamagedDatabase:
                return For(StartupFailureReason.DatabaseDamaged);
            case DatabaseUpgradeFailure.BackupFailed:
                return For(StartupFailureReason.BackupBeforeUpgradeFailed);
            default:
                return MigrationFailed(exception);
        }
    }

    private static StartupFailure MigrationFailed(DatabaseUpgradeException exception)
    {
        var message = For(StartupFailureReason.MigrationFailed).Message;
        message += exception.LiveDatabaseVerified
            ? " Databasen er kontrollert etter feilen: den består SQLites egne kontroller og har samme versjon som før. Innholdet er ikke sammenlignet rad for rad."
            : " Databasen kunne ikke bekreftes uendret etter feilen.";
        message += exception.BackupPath != null
            ? $" En sikkerhetskopi fra før oppgraderingen er lagret her: {exception.BackupPath} (mappen «{DatabaseUpgrader.BackupFolderName}»). Ikke slett den. Programmet gjenoppretter ikke noe automatisk."
            : " Det finnes ingen sikkerhetskopi fra før oppgraderingen.";

        return new StartupFailure(StartupFailureReason.MigrationFailed, message);
    }
}
