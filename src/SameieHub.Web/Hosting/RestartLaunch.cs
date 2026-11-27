using System.Diagnostics;

namespace SameieHub.Web.Hosting;

/// <summary>
/// Starting the program again after it has stopped itself. The new program is started only when the old one has already let go of the
/// instance lock and the database, and it waits for the old process to end before it does anything. A restart is never started by the
/// new program: only a person's choice in the interface can cause one, so a failure cannot lead to a loop.
/// </summary>
internal static class RestartLaunch
{
    public const string SwitchPrefix = "--restarted-after=";

    private const string ProgramFileName = "SameieHub.exe";

    /// <summary>Only the program file itself can start again. The dotnet tool (<c>dotnet run</c>) and the test host cannot.</summary>
    public static bool CanStartItself(string? processPath)
    {
        return !string.IsNullOrEmpty(processPath)
            && string.Equals(Path.GetFileName(processPath), ProgramFileName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The same arguments as this start, so a test database or another setting carries over, plus the number of the process that stops.</summary>
    public static string[] BuildArguments(IEnumerable<string> currentArguments, int processId)
    {
        return currentArguments
            .Where(argument => !argument.StartsWith(SwitchPrefix, StringComparison.Ordinal))
            .Append($"{SwitchPrefix}{processId}")
            .ToArray();
    }

    /// <summary>The process this start must wait for, or null for an ordinary start. A value that cannot be a process number is ignored.</summary>
    public static int? GetPreviousProcessId(string[] arguments)
    {
        var argument = arguments.FirstOrDefault(value => value.StartsWith(SwitchPrefix, StringComparison.Ordinal));
        if (argument == null)
        {
            return null;
        }

        var text = argument[SwitchPrefix.Length..];

        return int.TryParse(text, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var id) && id > 0 ? id : null;
    }

    /// <summary>Waits until the process has ended. True when it has ended or does not exist, false when it was still running after <paramref name="timeout"/>.</summary>
    public static bool WaitForExit(int processId, TimeSpan timeout)
    {
        try
        {
            using var process = Process.GetProcessById(processId);

            return process.WaitForExit(timeout);
        }
        catch (ArgumentException)
        {
            // No process has this number: it has already ended.
            return true;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // The process exists, but this account may not wait for it. It is not known to be gone.
            return false;
        }
    }

    /// <summary>
    /// Starts the program file with the arguments. It shares the console window of this program, so the window the person has open
    /// stays open for the new program. False when it could not be started.
    /// </summary>
    public static bool TryStart(string programPath, IEnumerable<string> arguments, ILogger logger)
    {
        var startInfo = new ProcessStartInfo(programPath)
        {
            UseShellExecute = false,
            WorkingDirectory = AppContext.BaseDirectory
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        try
        {
            using var started = Process.Start(startInfo);

            return started != null;
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            logger.LogError("The program could not be started again ({ExceptionType}).", exception.GetType().Name);

            return false;
        }
    }
}
