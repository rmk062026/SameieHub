<#
.SYNOPSIS
Creates and starts the fictional Fiktivdalen Sameie demo database. The ordinary database is never touched.

.EXAMPLE
.\scripts\demo.ps1 create   # creates the demo database (safe to run again)
.\scripts\demo.ps1 start    # starts SameieHub against the demo database
.\scripts\demo.ps1 reset    # deletes the demo database and creates it again
.\scripts\demo.ps1 info     # shows which database files the demo and the ordinary program use
#>
param(
    [Parameter(Position = 0)]
    [ValidateSet('create', 'start', 'reset', 'info')]
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

switch ($Action) {
    'create' {
        New-DemoDatabase
        Show-Info
    }
    'start' {
        if (-not (Test-Path $demoDatabase)) { throw 'The demo database does not exist. Run: .\scripts\demo.ps1 create' }
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
