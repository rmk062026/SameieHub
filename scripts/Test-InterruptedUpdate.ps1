<#
.SYNOPSIS
Tests what happens when an update fails half-way, and that installing the same version again repairs it. The update is made to fail on
purpose in a safe way: a file in the (temporary) installation folder is held open, so the installer can neither delete nor replace it.
Inno Setup removes the old program files before it copies the new ones, so after such a failure the program folder is incomplete.
The test then checks what the user has left (the data, always), and that running the installer again gives a working program.

Everything happens in a temporary folder and a temporary database. The installer only knows the real data folder
(%LOCALAPPDATA%\SameieHub), which it never changes; the test compares that folder before and after. It refuses to run if SameieHub is
already installed for this user.

.EXAMPLE
.\scripts\Test-InterruptedUpdate.ps1 -OldSetup .\artifacts\installer\SameieHub-0.8.4-test.9-win-x64-Setup.exe -Setup .\artifacts\installer\SameieHub-0.8.5-test.9-win-x64-Setup.exe
#>
param(
    [Parameter(Mandatory)][string]$OldSetup,
    [Parameter(Mandatory)][string]$Setup
)

$ErrorActionPreference = 'Stop'

$appId = '8D3F6B2A-5C1E-4B7A-9E64-2F0A7C1D5E93'
$uninstallKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{$appId}_is1"
$OldSetup = (Resolve-Path $OldSetup).Path
$Setup = (Resolve-Path $Setup).Path
if (Test-Path $uninstallKey) { throw 'SameieHub is already installed for this user. Uninstall it first. The test will not touch an existing installation.' }

$work = Join-Path ([IO.Path]::GetTempPath()) "SameieHubInterruptedTest-$([guid]::NewGuid().ToString('N'))"
$installDir = Join-Path $work 'app'
$dataFolder = Join-Path $work 'data'
$database = Join-Path $dataFolder 'sameiehub.db'
$realData = Join-Path $env:LOCALAPPDATA 'SameieHub'
$failures = New-Object System.Collections.Generic.List[string]
$checks = 0
$lock = $null

function Assert-That([bool]$Condition, [string]$Description) {
    $script:checks++
    if ($Condition) { Write-Host "  ok    $Description" } else { Write-Host "  FAIL  $Description" -ForegroundColor Red; $script:failures.Add($Description) }
}

function Get-Snapshot([string]$Folder) {
    if (-not (Test-Path $Folder)) { return '' }
    return (@(Get-ChildItem $Folder -Recurse -Force -File | Sort-Object FullName | ForEach-Object { "$($_.FullName.Substring($Folder.Length))|$($_.Length)|$($_.LastWriteTimeUtc.Ticks)" }) -join "`n")
}

function Invoke-Setup([string]$File) {
    $env:Database__Path = $database
    try {
        $process = Start-Process $File -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/SP-', "/DIR=$installDir", "/LOG=$(Join-Path $work 'setup.log')" -Wait -PassThru
    }
    finally { Remove-Item Env:Database__Path -ErrorAction SilentlyContinue }

    return $process.ExitCode
}

function Wait-Until([scriptblock]$Condition, [int]$Seconds = 60) {
    $deadline = (Get-Date).AddSeconds($Seconds)
    while ((Get-Date) -lt $deadline) { if (& $Condition) { return $true }; Start-Sleep -Milliseconds 300 }

    return [bool](& $Condition)
}

function Start-Program {
    $variables = @{ Database__Path = $database }
    foreach ($name in $variables.Keys) { Set-Item "Env:$name" $variables[$name] }
    try { return Start-Process (Join-Path $installDir 'SameieHub.exe') -ArgumentList '--unattended' -WorkingDirectory $work -PassThru }
    finally { foreach ($name in $variables.Keys) { Remove-Item "Env:$name" -ErrorAction SilentlyContinue } }
}

try {
    New-Item -ItemType Directory -Force $work, $dataFolder | Out-Null
    $before = Get-Snapshot $realData

    Write-Host '1. Install the old version and use it once (so there is a database)'
    Assert-That ((Invoke-Setup $OldSetup) -eq 0) 'the old version installs'
    $program = Start-Program
    Assert-That (Wait-Until { (Test-Path $database) -and ((Get-ChildItem (Join-Path $dataFolder 'logs') -Filter *.log -ErrorAction SilentlyContinue | Select-String 'running on http') -ne $null) } 90) 'the old version starts and makes its database'
    $env:Database__Path = $database
    try { Start-Process (Join-Path $installDir 'SameieHub.exe') -ArgumentList '--stop' -Wait -WindowStyle Hidden | Out-Null } finally { Remove-Item Env:Database__Path }
    Assert-That (Wait-Until { -not (Get-Process -Id $program.Id -ErrorAction SilentlyContinue) } 30) 'the program is stopped'
    $databaseBefore = (Get-FileHash $database).Hash
    $stateBefore = Get-Snapshot $dataFolder

    Write-Host '2. An update that fails half-way (a program file is held open)'
    $held = Join-Path $installDir 'SameieHub.dll'
    $lock = [IO.File]::Open($held, 'Open', 'Read', 'None')
    $exit = Invoke-Setup $Setup
    Write-Host "  note  the installer ended with exit code $exit"
    Assert-That ($exit -ne 0) 'the installer reports that it failed (not exit code 0)'
    $incomplete = -not (Test-Path (Join-Path $installDir 'SameieHub.exe')) -or ((Get-ChildItem $installDir -Filter *.dll).Count -lt 100)
    Write-Host ("  note  after the failure the folder has SameieHub.exe: {0}, {1} dll files" -f (Test-Path (Join-Path $installDir 'SameieHub.exe')), (Get-ChildItem $installDir -Filter *.dll).Count)
    Assert-That $incomplete 'the program folder is incomplete after the failure (this is the risk that is being tested)'
    Assert-That ((Get-FileHash $database).Hash -eq $databaseBefore) 'the database is untouched by the failed update'
    Assert-That ((Get-Snapshot $dataFolder) -eq $stateBefore) 'the data folder is untouched by the failed update'
    Assert-That ((Get-Snapshot $realData) -eq $before) 'the real data folder (%LOCALAPPDATA%\SameieHub) is untouched'

    Write-Host '3. Repair: install the same new version again'
    $lock.Dispose(); $lock = $null
    Assert-That ((Invoke-Setup $Setup) -eq 0) 'the same installer, run again, finishes with exit code 0'
    Assert-That (Test-Path (Join-Path $installDir 'SameieHub.exe')) 'SameieHub.exe is back'
    Assert-That ((Get-ChildItem $installDir -Filter *.dll).Count -ge 100) 'the program files are complete again'
    Assert-That ((Get-ItemProperty $uninstallKey).DisplayVersion -eq ((Split-Path $Setup -Leaf) -replace '^SameieHub-(.+)-win-x64-Setup\.exe$', '$1')) 'the installed version is the new one'
    $program = Start-Program
    Assert-That (Wait-Until { @(Get-Process -Id $program.Id -ErrorAction SilentlyContinue).Count -eq 1 -and ((Get-ChildItem (Join-Path $dataFolder 'logs') -Filter *.log | Select-String 'running on http').Count -ge 2) } 90) 'the repaired program starts on the old database'
    $env:Database__Path = $database
    try { Start-Process (Join-Path $installDir 'SameieHub.exe') -ArgumentList '--stop' -Wait -WindowStyle Hidden | Out-Null } finally { Remove-Item Env:Database__Path }
    Assert-That (Wait-Until { -not (Get-Process -Id $program.Id -ErrorAction SilentlyContinue) } 30) 'and stops normally'
    Assert-That ((Get-Snapshot $realData) -eq $before) 'the real data folder is still untouched'
}
catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
    $failures.Add($_.Exception.Message)
}
finally {
    if ($lock) { $lock.Dispose() }
    Get-Process -Name SameieHub -ErrorAction SilentlyContinue | Where-Object { $_.Path -and $_.Path.StartsWith($installDir, [StringComparison]::OrdinalIgnoreCase) } | Stop-Process -Force -ErrorAction SilentlyContinue
    $uninstaller = Join-Path $installDir 'unins000.exe'
    if (Test-Path $uninstaller) {
        Start-Process $uninstaller -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART' | Out-Null
        Wait-Until { -not (Test-Path $uninstallKey) -and -not (Test-Path $uninstaller) } 120 | Out-Null
    }
    $shortcut = Join-Path ([Environment]::GetFolderPath('Programs')) 'SameieHub.lnk'
    if (-not (Test-Path $uninstallKey) -and (Test-Path $shortcut)) { Remove-Item $shortcut -Force -ErrorAction SilentlyContinue }
    Start-Sleep -Milliseconds 500
    Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host ''
if ($failures.Count -gt 0) { Write-Host "$($failures.Count) of $checks checks failed." -ForegroundColor Red; exit 1 }
Write-Host "All $checks checks passed."
exit 0
