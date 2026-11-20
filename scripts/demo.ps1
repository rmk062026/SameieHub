<#
.SYNOPSIS
Creates and starts the fictional Fiktivdalen Sameie demo database. The ordinary database is never touched.

.EXAMPLE
.\scripts\demo.ps1 create   # creates the demo database (safe to run again)
.\scripts\demo.ps1 start    # starts SameieHub against the demo database
.\scripts\demo.ps1 launch   # creates the demo if it is missing, starts it and opens the browser when it is ready
.\scripts\demo.ps1 reset    # deletes the demo database and creates it again
.\scripts\demo.ps1 info     # shows which database files the demo and the ordinary program use
#>
param(
    [Parameter(Position = 0)]
    [ValidateSet('create', 'start', 'launch', 'reset', 'info')]
    [string]$Action = 'info'
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src\SameieHub.Web'
$demoDatabase = Join-Path $env:LOCALAPPDATA 'SameieHubDemo\sameiehub-demo.db'
$ordinaryDatabase = Join-Path $env:LOCALAPPDATA 'SameieHub\sameiehub.db'
$demoUrl = 'http://localhost:5100'

function Invoke-WithDemoDatabase([scriptblock]$Command) {
    # The environment variables are restored afterwards, so a later normal start uses the ordinary database.
    $oldPath = $env:Database__Path
    $oldUrls = $env:ASPNETCORE_URLS
    $oldEnvironment = $env:ASPNETCORE_ENVIRONMENT
    try {
        $env:Database__Path = $demoDatabase
        $env:ASPNETCORE_URLS = $demoUrl
        $env:ASPNETCORE_ENVIRONMENT = 'Development'
        & $Command
    }
    finally {
        $env:Database__Path = $oldPath
        $env:ASPNETCORE_URLS = $oldUrls
        $env:ASPNETCORE_ENVIRONMENT = $oldEnvironment
    }
}

function New-DemoDatabase {
    Invoke-WithDemoDatabase { dotnet run --project $project --no-launch-profile -- --seed-demo }
    if ($LASTEXITCODE -ne 0) { throw 'The demo database was not created. See the message above.' }
}

function Assert-DotnetAvailable {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw 'The .NET SDK (the dotnet command) was not found. Install the .NET 10 SDK, then try again.'
    }
}

# Looks at the demo database and changes nothing. Exit code 2 from the program means: an older demo without charging data.
function Test-DemoIsOutdated {
    # Out-Host keeps the program's own message on screen and out of the function's return value.
    Invoke-WithDemoDatabase { dotnet run --project $project --no-launch-profile -- --check-demo | Out-Host }

    return $LASTEXITCODE -eq 2
}

function Test-DemoPortInUse {
    return [bool](Get-NetTCPConnection -LocalPort 5100 -State Listen -ErrorAction SilentlyContinue)
}

function Show-Info {
    Write-Host "Demo database:     $demoDatabase  (exists: $(Test-Path $demoDatabase))"
    Write-Host "Ordinary database: $ordinaryDatabase  (exists: $(Test-Path $ordinaryDatabase))"
    Write-Host "Demo address:      $demoUrl"
}

# The SQLite files that belong to the demo database. Nothing else is ever deleted.
function Get-DemoDatabaseFiles {
    return @($demoDatabase, "$demoDatabase-shm", "$demoDatabase-wal", "$demoDatabase-journal")
}

function Test-DemoDatabaseInUse {
    $listening = Get-NetTCPConnection -LocalPort 5100 -State Listen -ErrorAction SilentlyContinue
    if ($listening) { return $true }

    # SQLite keeps the file open while the program runs, so an exclusive open fails then.
    foreach ($file in Get-DemoDatabaseFiles) {
        if (-not (Test-Path $file)) { continue }
        try {
            $stream = [System.IO.File]::Open($file, 'Open', 'ReadWrite', 'None')
            $stream.Dispose()
        }
        catch {
            return $true
        }
    }

    return $false
}

function Assert-IsDemoDatabasePath {
    $fullDemo = [System.IO.Path]::GetFullPath($demoDatabase)
    $fullOrdinary = [System.IO.Path]::GetFullPath($ordinaryDatabase)
    $folderName = Split-Path -Leaf (Split-Path -Parent $fullDemo)
    $fileName = Split-Path -Leaf $fullDemo

    if ($fullDemo -eq $fullOrdinary -or $folderName -ne 'SameieHubDemo' -or $fileName -ne 'sameiehub-demo.db') {
        throw "Refusing to continue: $fullDemo is not the dedicated demo database."
    }
}

function Remove-DemoDatabase {
    Assert-IsDemoDatabasePath
    if (Test-DemoDatabaseInUse) {
        throw 'The demo database is in use. Stop the demo (Ctrl+C in the window where it runs) and try again.'
    }

    foreach ($file in Get-DemoDatabaseFiles) {
        if (Test-Path $file) { Remove-Item -LiteralPath $file -Force }
    }
}

# Creates the demo if it is missing, starts the program and opens the browser when the server answers.
function Start-DemoWithBrowser {
    Assert-DotnetAvailable

    if (Test-DemoPortInUse) {
        throw "Port 5100 is already in use. Is the demo already running? Open $demoUrl, or close the program that uses the port and try again."
    }

    if (-not (Test-Path $demoDatabase)) {
        Write-Host 'The demo database does not exist yet. Creating it ...'
        New-DemoDatabase
    }
    elseif (Test-DemoIsOutdated) {
        Write-Warning 'The existing demo is older and has no charging facility. It is started as it is. For the new demo, close this window and run Nullstill-demo.cmd.'
    }

    Show-Info
    Write-Host 'Starting SameieHub in demo mode. This can take a little while the first time ...'

    $process = Invoke-WithDemoDatabase {
        Start-Process -FilePath 'dotnet' -ArgumentList @('run', '--project', "`"$project`"", '--no-launch-profile') -NoNewWindow -PassThru
    }

    try {
        $deadline = (Get-Date).AddSeconds(180)
        $ready = $false
        while (-not $ready) {
            if ($process.HasExited) {
                throw "SameieHub stopped during startup (exit code $($process.ExitCode)). See the message above."
            }
            if ((Get-Date) -gt $deadline) {
                throw 'SameieHub did not answer within 3 minutes. See the message above.'
            }
            try {
                $response = Invoke-WebRequest -Uri $demoUrl -UseBasicParsing -TimeoutSec 2
                $ready = $response.StatusCode -eq 200
            }
            catch {
                Start-Sleep -Milliseconds 500
            }
        }

        Start-Process $demoUrl
        Write-Host "The demo is running at $demoUrl. Close this window or press Ctrl+C to stop it."
        $process.WaitForExit()
    }
    finally {
        if ($process -and -not $process.HasExited) {
            # Also stops the program that dotnet run started.
            & taskkill /PID $process.Id /T /F | Out-Null
        }
    }
}

switch ($Action) {
    'create' {
        New-DemoDatabase
        Show-Info
    }
    'launch' {
        try {
            Start-DemoWithBrowser
        }
        catch {
            Write-Host ''
            Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
            exit 1
        }
    }
    'start' {
        if (-not (Test-Path $demoDatabase)) { throw 'The demo database does not exist. Run: .\scripts\demo.ps1 create' }
        if (Test-DemoIsOutdated) {
            Write-Warning 'The existing demo is older and has no charging facility. To get the new demo, run: .\scripts\demo.ps1 reset'
        }
        Show-Info
        Write-Host "Starting SameieHub in demo mode. Press Ctrl+C to stop."
        Invoke-WithDemoDatabase { dotnet run --project $project --no-launch-profile }
    }
    'reset' {
        Remove-DemoDatabase
        New-DemoDatabase
        Show-Info
    }
    'info' { Show-Info }
}
