<#
.SYNOPSIS
Smoke test of the internal Windows test package. It unpacks the zip to a temporary folder, starts SameieHub.exe the way a double-click
does (no flags) against a temporary database, checks it, stops it the way a person would, and checks that nothing is left behind.
The ordinary database in AppData is never used, and the test data is empty.

.EXAMPLE
.\scripts\Test-WindowsPackage.ps1 -Zip .\artifacts\windows\SameieHub-0.8.2-test-win-x64-test.zip
.\scripts\Test-WindowsPackage.ps1 -Zip ... -StopWith close   # the X button of the console window (needs a classic console window)
#>
param(
    [Parameter(Mandatory)][string]$Zip,
    [ValidateSet('ctrlc', 'close')][string]$StopWith = 'ctrlc'
)

$ErrorActionPreference = 'Stop'

$work = Join-Path ([IO.Path]::GetTempPath()) "SameieHubPackageTest-$([guid]::NewGuid().ToString('N'))"
$databaseFolder = Join-Path $work 'data'
$database = Join-Path $databaseFolder 'sameiehub.db'
$failures = New-Object System.Collections.Generic.List[string]
$checks = 0

function Assert-That([bool]$Condition, [string]$Description) {
    $script:checks++
    if ($Condition) { Write-Host "  ok    $Description" }
    else { Write-Host "  FAIL  $Description" -ForegroundColor Red; $script:failures.Add($Description) }
}

function Get-RunningProgram([string]$Folder) {
    return @(Get-Process -Name SameieHub -ErrorAction SilentlyContinue | Where-Object { $_.Path -and $_.Path.StartsWith($Folder, [StringComparison]::OrdinalIgnoreCase) })
}

$process = $null
$started = $null
try {
    New-Item -ItemType Directory -Force $work, $databaseFolder | Out-Null
    $extracted = Join-Path $work 'package'
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::ExtractToDirectory((Resolve-Path $Zip).Path, $extracted)
    $program = Get-ChildItem $extracted -Filter SameieHub.exe -Recurse | Select-Object -First 1
    Assert-That ($null -ne $program) 'SameieHub.exe is in the zip'
    if (-not $program) { throw 'SameieHub.exe is missing.' }
    $folder = $program.DirectoryName

    Write-Host 'Package contents'
    foreach ($name in 'SameieHub.dll', 'SameieHub.deps.json', 'SameieHub.runtimeconfig.json', 'appsettings.json', 'e_sqlite3.dll', 'hostfxr.dll', 'coreclr.dll',
        'SameieHub.staticwebassets.endpoints.json', 'wwwroot\app.css', 'LES-MEG.txt', 'Start-SameieHub.cmd') {
        Assert-That (Test-Path (Join-Path $folder $name)) "$name is included"
    }
    Assert-That (-not (Test-Path (Join-Path $folder 'appsettings.Development.json'))) 'no development settings'
    $unwanted = @(Get-ChildItem $folder -Recurse -File | Where-Object { $_.Extension -in '.db', '.sqlite', '.log', '.pdb', '.zip', '.pfx', '.key', '.env' -or $_.Name -match '^(secrets?|appsettings\.secrets?)\.json$' })
    Assert-That ($unwanted.Count -eq 0) ('no database, log, symbol, key or secret files' + $(if ($unwanted.Count -gt 0) { ': ' + ($unwanted.Name -join ', ') }))
    $runtimeConfig = Get-Content (Join-Path $folder 'SameieHub.runtimeconfig.json') -Raw
    Assert-That ($runtimeConfig -match 'includedFrameworks') 'the .NET runtime is part of the folder (self-contained)'

    Write-Host "Start ($StopWith)"
    # No --windows-host: the published program has to choose the Windows mode itself. The database path is set by environment variable.
    $env:Database__Path = $database
    try {
        $arguments = @('--unattended')
        if ($StopWith -eq 'close') {
            # A classic console window, so the X button can be tested.
            $null = Start-Process conhost.exe -ArgumentList (@("`"$($program.FullName)`"") + $arguments) -WorkingDirectory $work -PassThru
        }
        else {
            $started = Start-Process $program.FullName -ArgumentList $arguments -WorkingDirectory $work -PassThru
        }
    }
    finally {
        Remove-Item Env:Database__Path
    }

    $address = $null
    $deadline = (Get-Date).AddSeconds(90)
    while (-not $address -and (Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 500
        $log = Get-ChildItem (Join-Path $databaseFolder 'logs') -Filter *.log -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($log) {
            $match = Select-String -Path $log.FullName -Pattern 'running on (http://127\.0\.0\.1:\d+)' | Select-Object -First 1
            if ($match) { $address = $match.Matches[0].Groups[1].Value }
        }
    }
    Assert-That ($null -ne $address) 'the program started and logged its address'
    if (-not $address) { throw 'The program did not start.' }

    $running = Get-RunningProgram $folder
    Assert-That ($running.Count -eq 1) 'exactly one SameieHub process is running'
    $process = $running[0]
    Write-Host "  address $address (process $($process.Id))"

    Write-Host 'Network'
    $listeners = @(Get-NetTCPConnection -OwningProcess $process.Id -State Listen -ErrorAction SilentlyContinue)
    Assert-That ($listeners.Count -ge 1) 'the program listens on a port'
    Assert-That (@($listeners | Where-Object { $_.LocalAddress -ne '127.0.0.1' }).Count -eq 0) 'every listening address is 127.0.0.1 (no network, no ::1)'

    $client = New-Object System.Net.Http.HttpClient
    $response = $client.GetAsync($address).GetAwaiter().GetResult()
    $html = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    Assert-That ($response.StatusCode -eq 200) 'the front page answers 200'
    Assert-That ($html -match 'SameieHub') 'the front page is SameieHub'
    $request = New-Object System.Net.Http.HttpRequestMessage ([System.Net.Http.HttpMethod]::Get), $address
    $request.Headers.Host = 'evil.example'
    Assert-That ($client.SendAsync($request).GetAwaiter().GetResult().StatusCode -eq 400) 'a foreign host name is refused (400)'

    $assets = @([regex]::Matches($html, '(?:href|src)="(?<path>[^"]+\.(?:css|js))"') | ForEach-Object { $_.Groups['path'].Value } | Sort-Object -Unique)
    Assert-That ($assets.Count -ge 3) "the page links its style sheets and scripts ($($assets.Count) found)"
    foreach ($asset in $assets) {
        $status = $client.GetAsync($address.TrimEnd('/') + '/' + $asset.TrimStart('/')).GetAwaiter().GetResult().StatusCode
        Assert-That ($status -eq 200) "static file $asset answers 200"
    }

    Write-Host 'A second start'
    $env:Database__Path = $database
    try {
        $second = Start-Process $program.FullName -ArgumentList '--unattended' -WorkingDirectory $work -PassThru -Wait
    }
    finally {
        Remove-Item Env:Database__Path
    }
    Assert-That ($second.ExitCode -eq 1) 'a second start stops with exit code 1 (the program already runs)'
    Assert-That ((Get-RunningProgram $folder).Count -eq 1) 'still exactly one SameieHub process'

    Write-Host "Stop with $StopWith"
    $signal = Join-Path $PSScriptRoot 'Send-ConsoleSignal.ps1'
    & powershell -NoProfile -File $signal -ProcessId $process.Id -Mode $StopWith | ForEach-Object { Write-Host "  $_" }
    $exited = $process.WaitForExit(20000)
    Assert-That $exited 'the program stopped within 20 seconds'
    Start-Sleep -Milliseconds 500
    Assert-That ((Get-RunningProgram $folder).Count -eq 0) 'no SameieHub process is left'
    if ($exited -and $started) { Assert-That ($started.ExitCode -eq 0) "exit code 0 (was $($started.ExitCode))" }
    Assert-That (-not (Test-Path "$database-wal")) 'the WAL file is merged and gone (the connections were closed)'
    Assert-That (-not (Test-Path "$database-shm")) 'the shared-memory file is gone'
    $lockFree = $true
    try { $lock = [IO.File]::Open((Join-Path $databaseFolder 'sameiehub.lock'), 'Open', 'ReadWrite', 'None'); $lock.Dispose() } catch { $lockFree = $false }
    Assert-That $lockFree 'the instance lock is free'
    $databaseFree = $true
    try { $stream = [IO.File]::Open($database, 'Open', 'ReadWrite', 'None'); $stream.Dispose() } catch { $databaseFree = $false }
    Assert-That $databaseFree 'the database can be opened again'
    $logText = Get-Content (Get-ChildItem (Join-Path $databaseFolder 'logs') -Filter *.log | Select-Object -First 1).FullName -Raw
    Assert-That ($logText -match 'shutting down') 'the log shows an orderly shutdown'
    Assert-That ($logText -notmatch [regex]::Escape($work)) 'the log contains no folder path'
}
catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
    $failures.Add($_.Exception.Message)
}
finally {
    # Whatever happened, no program from this test is left running, and the temporary folder is removed.
    if (Test-Path $work) {
        Get-RunningProgram $work | Stop-Process -Force -ErrorAction SilentlyContinue
        Start-Sleep -Milliseconds 500
        Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Write-Host ''
if ($failures.Count -gt 0) {
    Write-Host "$($failures.Count) of $checks checks failed." -ForegroundColor Red
    exit 1
}

Write-Host "All $checks checks passed."

exit 0
