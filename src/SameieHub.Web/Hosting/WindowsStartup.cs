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
    DatabaseFileEmpty,
    BackupBeforeUpgradeFailed,
    MigrationFailed,
    DemoDatabaseNotAllowed,
    ServerCouldNotStart,
    UnexpectedError,
    SetupInProgress,
    UnsafeNetworkBinding
}

/// <param name="Reason">What stopped the start. Only this is logged, never the message, which can contain a path.</param>
/// <param name="Message">What the person is told, in Norwegian.</param>
internal sealed record StartupFailure(StartupFailureReason Reason, string Message);

/// <summary>Either the lock the program must hold while it runs, or the reason it cannot start.</summary>
internal sealed class StartupResult : IDisposable
{
    public StartupResult(InstanceLock? instanceLock, StartupFailure? failure, bool activatedRunningInstance = false)
    {
        Lock = instanceLock;
        Failure = failure;
        ActivatedRunningInstance = activatedRunningInstance;
    }

    /// <summary>The program was already running and was asked to open the browser. This start has nothing more to do.</summary>
    public bool ActivatedRunningInstance { get; }

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
        Func<Task> upgradeDatabase,
        ILogger logger,
        Func<Task<bool>>? activateRunningInstance = null)
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
            // Another instance holds the lock. It is asked to open the browser; nothing here touches the database or starts a server.
            if (activateRunningInstance != null && await activateRunningInstance())
            {
                logger.LogInformation("The running program was asked to open the browser.");

                return new StartupResult(null, null, activatedRunningInstance: true);
            }

            return Failed(StartupFailureReason.AlreadyRunning, logger);
        }

        // From here on the lock is held, so every way out except success must release it.
        var failure = await RunStepsAsync(restore, upgradeDatabase, logger);
        if (failure != null)
        {
            instanceLock.Dispose();

            return new StartupResult(null, failure);
        }

        return new StartupResult(instanceLock, null);
    }

    private static async Task<StartupFailure?> RunStepsAsync(IDatabaseRestore restore, Func<Task> upgradeDatabase, ILogger logger)
    {
        RestoreOutcome restoreOutcome;
        try
        {
            restoreOutcome = restore.ApplyPendingRestore();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The restore engine is not meant to throw. If it does, the state of the database files is uncertain, so the program
            // stops before it migrates, which could create a new, empty database.
            logger.LogError("The restore at startup failed unexpectedly ({ExceptionType}).", exception.GetType().Name);

            return Report(StartupMessages.For(StartupFailureReason.RestoreUnresolved), logger);
        }

        if (restoreOutcome.Kind != RestoreOutcomeKind.NothingPending)
        {
            logger.LogWarning("Database restore at startup: {Result}.", restoreOutcome.Kind);
        }

        if (restoreOutcome.Kind == RestoreOutcomeKind.Unresolved)
        {
            return Report(StartupMessages.For(StartupFailureReason.RestoreUnresolved), logger);
        }

        try
        {
            await upgradeDatabase();
        }
        catch (DatabaseUpgradeException exception)
        {
            return Report(StartupMessages.For(exception), logger);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Anything else (a locked file, a full disk) must still end in a readable message, not a crash with a stack trace.
            logger.LogError("The database could not be prepared ({ExceptionType}).", exception.GetType().Name);

            return Report(StartupMessages.For(StartupFailureReason.UnexpectedError), logger);
        }

        return null;
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
            StartupFailureReason.DatabaseFileEmpty =>
                "Databasefilen er tom (0 byte). Programmet har ikke endret eller erstattet den. Hvis du vet at dette er en ny installasjon, kan du flytte filen til side og starte på nytt. Ellers: gjenopprett fra en sikkerhetskopi, eller få hjelp.",
            StartupFailureReason.DatabaseDamaged =>
                "Databasefilen er skadet eller kan ikke leses. Programmet har ikke endret den. Gjenopprett fra en sikkerhetskopi, eller få hjelp.",
            StartupFailureReason.BackupBeforeUpgradeFailed =>
                "SameieHub kunne ikke lage en sikkerhetskopi før databasen skulle oppgraderes, og stoppet derfor uten å oppgradere. Kontroller at det er ledig plass og at mappen med databasen kan skrives til.",
            StartupFailureReason.MigrationFailed =>
                "Databasen kunne ikke oppgraderes. Programmet er stoppet.",
            StartupFailureReason.DemoDatabaseNotAllowed =>
                "Demodatabasen kan ikke brukes i Windows-modus. Start demoen med Start-demo.cmd.",
            StartupFailureReason.UnexpectedError =>
                "SameieHub kunne ikke starte på grunn av en uventet feil. Programmet har ikke gjort flere endringer i databasen. Se loggfilen i mappen «logs» ved siden av databasen.",
            StartupFailureReason.SetupInProgress =>
                "En installasjon eller avinstallasjon av SameieHub pågår. Vent til den er ferdig, og start SameieHub på nytt.",
            StartupFailureReason.UnsafeNetworkBinding =>
                "SameieHub ble stoppet fordi serveren lyttet på en annen adresse enn 127.0.0.1 (en innstilling la til en nettverksadresse). Ingenting er endret. Fjern innstillingen og start på nytt.",
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
            case DatabaseUpgradeFailure.EmptyDatabaseFile:
                return For(StartupFailureReason.DatabaseFileEmpty);
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
