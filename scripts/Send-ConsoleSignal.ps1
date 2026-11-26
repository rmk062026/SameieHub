<#
.SYNOPSIS
Sends Ctrl+C, or what the X button of the console window sends, to the console of another process. Used by Test-WindowsPackage.ps1
to check that SameieHub shuts down in an orderly way. Run it in its own PowerShell process: it detaches from the console it started in.

.EXAMPLE
powershell -NoProfile -File .\scripts\Send-ConsoleSignal.ps1 -ProcessId 1234 -Mode ctrlc
#>
param(
    [Parameter(Mandatory)][int]$ProcessId,
    [Parameter(Mandatory)][ValidateSet('ctrlc', 'close')][string]$Mode
)

Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class ConsoleSignal
{
    [DllImport("kernel32.dll", SetLastError = true)] public static extern bool FreeConsole();
    [DllImport("kernel32.dll", SetLastError = true)] public static extern bool AttachConsole(uint pid);
    [DllImport("kernel32.dll")] public static extern bool SetConsoleCtrlHandler(IntPtr handler, bool add);
    [DllImport("kernel32.dll")] public static extern bool GenerateConsoleCtrlEvent(uint ctrlEvent, uint group);
    [DllImport("kernel32.dll")] public static extern IntPtr GetConsoleWindow();
    [DllImport("user32.dll")] public static extern IntPtr SendMessageTimeout(IntPtr window, uint message, IntPtr w, IntPtr l, uint flags, uint timeout, out IntPtr result);
}
"@

[ConsoleSignal]::FreeConsole() | Out-Null
if (-not [ConsoleSignal]::AttachConsole([uint32]$ProcessId)) {
    Write-Output "Could not attach to the console of process $ProcessId (error $([Runtime.InteropServices.Marshal]::GetLastWin32Error()))."
    exit 2
}

if ($Mode -eq 'ctrlc') {
    # This process must not stop itself when it sends the signal to the shared console.
    [ConsoleSignal]::SetConsoleCtrlHandler([IntPtr]::Zero, $true) | Out-Null
    [ConsoleSignal]::GenerateConsoleCtrlEvent(0, 0) | Out-Null
    Write-Output 'Sent Ctrl+C.'
}
else {
    $window = [ConsoleSignal]::GetConsoleWindow()
    $result = [IntPtr]::Zero
    # SC_CLOSE is what the X button of the window sends.
    [ConsoleSignal]::SendMessageTimeout($window, 0x0112, [IntPtr]0xF060, [IntPtr]::Zero, 2, 3000, [ref]$result) | Out-Null
    Write-Output 'Sent SC_CLOSE to the console window.'
}
