<#
.SYNOPSIS
Tests the SameieHub installer on the real file system: installs, starts the installed program, updates it (also while it runs), refuses
to replace files while it cannot be stopped, uninstalls, and installs again, and checks after each step that the user's data is still there.
The ordinary database is never used: the installed program is started with Database__Path pointing into a temporary folder.

Safety: the installer only knows the real data folder (%LOCALAPPDATA%\SameieHub), so the test adds one fictional marker file there and
compares the folder before and after. It refuses to run if SameieHub is already installed for this user.

.EXAMPLE
.\scripts\Test-Installer.ps1 -Setup .\artifacts\installer\SameieHub-0.8.4-test.2-win-x64-Setup.exe -OldSetup .\artifacts\installer\SameieHub-0.8.4-test.1-win-x64-Setup.exe
.\scripts\Test-Installer.ps1 -Setup ... -OldSetup ... -UseDefaultDirectory   # CI: installs under %LOCALAPPDATA%\Programs\SameieHub
#>
param(
    [Parameter(Mandatory)][string]$Setup,
    [string]$OldSetup = '',
    [switch]$UseDefaultDirectory,
    [switch]$AllowExistingInstall
)

$ErrorActionPreference = 'Stop'

$appId = '8D3F6B2A-5C1E-4B7A-9E64-2F0A7C1D5E93'
$uninstallKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{$appId}_is1"
$Setup = (Resolve-Path $Setup).Path
if ($OldSetup) { $OldSetup = (Resolve-Path $OldSetup).Path }
function Get-VersionOf([string]$File) { if ((Split-Path $File -Leaf) -match '^SameieHub-(.+)-win-x64-Setup\.exe$') { return $Matches[1] } else { throw "Unexpected file name: $File" } }
$newVersion = Get-VersionOf $Setup

$work = Join-Path ([IO.Path]::GetTempPath()) "SameieHubInstallerTest-$([guid]::NewGuid().ToString('N'))"
$dataFolder = Join-Path $work 'data'
$database = Join-Path $dataFolder 'sameiehub.db'
$installDir = if ($UseDefaultDirectory) { Join-Path $env:LOCALAPPDATA 'Programs\SameieHub' } else { Join-Path $work 'app' }
$realData = Join-Path $env:LOCALAPPDATA 'SameieHub'
$marker = Join-Path $realData "installer-test-marker-$([guid]::NewGuid().ToString('N')).txt"
$shortcut = Join-Path ([Environment]::GetFolderPath('Programs')) 'SameieHub.lnk'
$failures = New-Object System.Collections.Generic.List[string]
$checks = 0
$holders = New-Object System.Collections.Generic.List[object]
$script:runsBefore = @{}
$realDataExisted = Test-Path $realData

function Assert-That([bool]$Condition, [string]$Description) {
    $script:checks++
    if ($Condition) { Write-Host "  ok    $Description" }
    else { Write-Host "  FAIL  $Description" -ForegroundColor Red; $script:failures.Add($Description) }
}

function Get-Snapshot([string]$Folder) {
    if (-not (Test-Path $Folder)) { return @() }
    return @(Get-ChildItem $Folder -Recurse -Force -File | Where-Object { $_.Name -notlike 'installer-test-marker-*' } |
        Sort-Object FullName | ForEach-Object { "$($_.FullName.Substring($Folder.Length))|$($_.Length)|$($_.LastWriteTimeUtc.Ticks)" })
}

function Test-DataUnchanged {
    # Joined to text, because comparing two empty lists (a data folder that does not exist yet, as on a clean CI machine) is an error in Compare-Object.
    return ((@(Get-Snapshot $realData) -join "`n") -eq (@($script:before) -join "`n"))
}

function Wait-Until([scriptblock]$Condition, [int]$Seconds = 60) {
    $deadline = (Get-Date).AddSeconds($Seconds)
    while ((Get-Date) -lt $deadline) { if (& $Condition) { return $true }; Start-Sleep -Milliseconds 300 }
    return [bool](& $Condition)
}

function Invoke-Setup([string]$File, [string[]]$Extra = @()) {
    $log = Join-Path $work "setup-$([guid]::NewGuid().ToString('N').Substring(0, 6)).log"
    $arguments = @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/SP-', "/LOG=$log") + $Extra
    if (-not $UseDefaultDirectory) { $arguments += "/DIR=$installDir" }
    # The installer starts "SameieHub.exe --stop" with this environment, so it must point at the test database like the running program does.
    $env:Database__Path = $database
    try { $process = Start-Process $File -ArgumentList $arguments -Wait -PassThru } finally { Remove-Item Env:Database__Path -ErrorAction SilentlyContinue }
    return $process.ExitCode
}

function Invoke-Uninstall {
    $uninstaller = Join-Path $installDir 'unins000.exe'
    if (-not (Test-Path $uninstaller)) { return $false }
    $env:Database__Path = $database
    try { Start-Process $uninstaller -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART' | Out-Null } finally { Remove-Item Env:Database__Path -ErrorAction SilentlyContinue }
    # The uninstaller starts a copy of itself and ends, so the real end is when the entry and the program are gone.
    return (Wait-Until { -not (Test-Path $uninstallKey) -and -not (Test-Path (Join-Path $installDir 'SameieHub.exe')) } 90)
}

function Get-RunningProgram { return @(Get-Process -Name SameieHub -ErrorAction SilentlyContinue | Where-Object { $_.Path -and $_.Path.StartsWith($installDir, [StringComparison]::OrdinalIgnoreCase) }) }

function Start-Program([string[]]$Arguments = @('--unattended'), [hashtable]$Environment = @{}) {
    $folder = if ($Environment.ContainsKey('Database__Path')) { Split-Path -Parent $Environment['Database__Path'] } else { $dataFolder }
    $script:runsBefore[$folder] = Get-RunCount $folder
    $variables = @{ Database__Path = $database; SAMEIEHUB_WINDOWS_HOST = '0'; Launch__UrlFile = (Join-Path $work 'launch-url.txt') }
    foreach ($name in $Environment.Keys) { $variables[$name] = $Environment[$name] }
    foreach ($name in $variables.Keys) { Set-Item "Env:$name" $variables[$name] }
    try { $process = Start-Process (Join-Path $installDir 'SameieHub.exe') -ArgumentList $Arguments -WorkingDirectory $work -PassThru }
    finally { foreach ($name in $variables.Keys) { Remove-Item "Env:$name" -ErrorAction SilentlyContinue } }
    return $process
}

function Get-RunCount([string]$Folder = $dataFolder) {
    $log = Get-ChildItem (Join-Path $Folder 'logs') -Filter *.log -ErrorAction SilentlyContinue
    if (-not $log) { return 0 }
    return @($log | ForEach-Object { Select-String -Path $_.FullName -Pattern 'running on http' }).Count
}

function Get-LoggedAddress([int]$Seconds = 60, [string]$Folder = $dataFolder) {
    $script:foundAddress = $null
    $startsBefore = $script:runsBefore[$Folder]
    Wait-Until {
        $log = Get-ChildItem (Join-Path $Folder 'logs') -Filter *.log -ErrorAction SilentlyContinue | Sort-Object Name | Select-Object -Last 1
        if ($log) {
            $hit = Select-String -Path $log.FullName -Pattern 'running on (http://127\.0\.0\.1:\d+)' | Select-Object -Last 1
            if ($hit) { $script:foundAddress = $hit.Matches[0].Groups[1].Value }
        }
        return ([bool]$script:foundAddress) -and ((Get-RunCount $Folder) -gt $startsBefore)
    } $Seconds | Out-Null
    return $script:foundAddress
}

function Stop-ProgramGently {
    # The same command the installer uses: the program shuts down the way Ctrl+C does.
    $env:Database__Path = $database
    try { $stop = Start-Process (Join-Path $installDir 'SameieHub.exe') -ArgumentList '--stop' -Wait -PassThru -WindowStyle Hidden } finally { Remove-Item Env:Database__Path }
    $gone = Wait-Until { (Get-RunningProgram).Count -eq 0 } 20
    return @{ ExitCode = $stop.ExitCode; Gone = $gone }
}

function Invoke-Sql([string]$Sql) {
    $script = Join-Path $work 'sql.py'
    if (-not (Test-Path $script)) { Set-Content $script "import sqlite3, sys`nc = sqlite3.connect(sys.argv[1])`nr = c.execute(sys.argv[2]).fetchall()`nc.commit()`nprint(r[0][0] if r else '')" }
    return (& python $script $database $Sql)
}

function Get-StatusOf([string]$Url) {
    $client = New-Object System.Net.Http.HttpClient ((New-Object System.Net.Http.HttpClientHandler -Property @{ UseCookies = $false; AllowAutoRedirect = $false }))
    return [int]$client.GetAsync($Url).GetAwaiter().GetResult().StatusCode
}

function Start-Holder([string]$MutexName) {
    $holder = Start-Process powershell -ArgumentList '-NoProfile', '-Command', "`$m = New-Object System.Threading.Mutex(`$false, '$MutexName'); Start-Sleep -Seconds 240" -PassThru -WindowStyle Hidden
    $script:holders.Add($holder)
    Start-Sleep -Seconds 2
    return $holder
}

try {
    if ((Test-Path $uninstallKey) -and -not $AllowExistingInstall) { throw 'SameieHub is already installed for this user. The test would update and remove it, so it stops. Uninstall it first.' }
    if ($UseDefaultDirectory -and (Test-Path $installDir) -and -not $AllowExistingInstall) { throw "$installDir already exists." }
    New-Item -ItemType Directory -Force $work, $dataFolder | Out-Null
    $script:before = @(Get-Snapshot $realData)
    New-Item -ItemType Directory -Force $realData | Out-Null
    Set-Content $marker 'Fictional marker made by Test-Installer.ps1. It is removed at the end.'
    $python = [bool](Get-Command python -ErrorAction SilentlyContinue)
    if (-not $python) { Write-Host '  note  python was not found, so the database rows are not checked' -ForegroundColor Yellow }

    $first = if ($OldSetup) { $OldSetup } else { $Setup }
    Write-Host "1. First installation ($(Get-VersionOf $first))"
    Assert-That ((Invoke-Setup $first) -eq 0) 'the installer ends with exit code 0'
    Assert-That (Test-Path (Join-Path $installDir 'SameieHub.exe')) 'SameieHub.exe is installed'
    Assert-That (Test-Path (Join-Path $installDir 'unins000.exe')) 'an uninstaller is installed'
    Assert-That ($installDir.StartsWith($env:LOCALAPPDATA, [StringComparison]::OrdinalIgnoreCase)) 'the program is installed under the user profile (no administrator rights needed)'
    $entry = Get-ItemProperty $uninstallKey -ErrorAction SilentlyContinue
    Assert-That ($null -ne $entry) 'the program is listed under Installed apps'
    Assert-That ($entry.DisplayVersion -eq (Get-VersionOf $first)) "the listed version is $(Get-VersionOf $first)"
    $shell = New-Object -ComObject WScript.Shell
    $link = if (Test-Path $shortcut) { $shell.CreateShortcut($shortcut) } else { $null }
    Assert-That ($null -ne $link) 'there is a Start menu shortcut'
    if ($link) {
        Assert-That ($link.TargetPath -eq (Join-Path $installDir 'SameieHub.exe')) 'the shortcut starts SameieHub.exe'
        Assert-That ([string]::IsNullOrEmpty($link.Arguments)) 'the shortcut gives the program no arguments'
    }
    Assert-That (-not (Test-Path (Join-Path ([Environment]::GetFolderPath('DesktopDirectory')) 'SameieHub.lnk')) -or $UseDefaultDirectory) 'no desktop shortcut unless asked for'
    $stray = @(Get-ChildItem $installDir -Recurse -File | Where-Object { $_.Extension -in '.db', '.sqlite', '.log', '.pdb', '.pfx', '.key' -or $_.Name -in 'LES-MEG.txt', 'Start-SameieHub.cmd' })
    Assert-That ($stray.Count -eq 0) ('no database, log, symbol, key or zip-only file in the program folder' + $(if ($stray.Count) { ': ' + ($stray.Name -join ', ') }))
    Assert-That (-not (Test-Path (Join-Path $realData 'sameiehub.db')) -or $realDataExisted) 'the installer did not create a database'
    Assert-That ((Test-DataUnchanged)) 'the installer did not add or remove files in the data folder'

    Write-Host '2. The installed program (the way a shortcut starts it, plus attempts to weaken it)'
    $app = Start-Program
    $address = Get-LoggedAddress
    Assert-That ($null -ne $address) 'the installed program starts and logs its address'
    if (-not $address) { throw 'The installed program did not start.' }
    Assert-That ((Get-RunningProgram).Count -eq 1) 'exactly one SameieHub process runs'
    Assert-That ((Get-Process -Id $app.Id).Path.StartsWith($installDir, [StringComparison]::OrdinalIgnoreCase)) 'it runs from the installed folder'
    $listeners = @(Get-NetTCPConnection -OwningProcess $app.Id -State Listen -ErrorAction SilentlyContinue)
    Assert-That (($listeners.Count -ge 1) -and (@($listeners | Where-Object { $_.LocalAddress -ne '127.0.0.1' }).Count -eq 0)) 'it listens on 127.0.0.1 only'
    Assert-That ((Get-StatusOf "$address/") -eq 403) 'the front page is refused without a session, although SAMEIEHUB_WINDOWS_HOST=0 was set'
    Assert-That ((Get-StatusOf "$address/oppgaver") -eq 403) 'a page is refused without a session'
    Assert-That ((Get-StatusOf "$address/start") -eq 200) 'the start address answers'
    Assert-That (-not (Test-Path (Join-Path $work 'launch-url.txt'))) 'Launch:UrlFile is ignored: no launch code was written to a file'
    $again = Start-Program
    Assert-That ($again.WaitForExit(30000) -and $again.ExitCode -eq 0) 'a second start asks the running program to open the browser and ends with exit code 0'
    Assert-That ((Get-RunningProgram).Count -eq 1) 'still exactly one SameieHub process'
    # Another database, so it can start next to the first. The arguments and settings below try to weaken the protection.
    $data2 = Join-Path $work 'data2'
    $weak = Start-Program @('--unattended', '--windows-host=false', '--urls=http://0.0.0.0:0') @{ Database__Path = (Join-Path $data2 'sameiehub.db') }
    $weakAddress = Get-LoggedAddress 60 $data2
    Assert-That ($null -ne $weakAddress) 'a start with weakening arguments still starts in the protected mode'
    if ($weakAddress) {
        $weakListeners = @(Get-NetTCPConnection -OwningProcess $weak.Id -State Listen -ErrorAction SilentlyContinue)
        Assert-That (($weakListeners.Count -ge 1) -and (@($weakListeners | Where-Object { $_.LocalAddress -ne '127.0.0.1' }).Count -eq 0)) 'it still listens on 127.0.0.1 only (--urls is ignored)'
        Assert-That ((Get-StatusOf "$weakAddress/") -eq 403) 'and it still refuses a page without a session'
    }
    Stop-Process -Id $weak.Id -Force -ErrorAction SilentlyContinue
    Wait-Until { -not [bool](Get-Process -Id $weak.Id -ErrorAction SilentlyContinue) } 10 | Out-Null

    Write-Host '3. Orderly stop (the command the installer uses)'
    $stopped = Stop-ProgramGently
    Assert-That ($stopped.ExitCode -eq 0) '--stop is accepted by the running program'
    Assert-That $stopped.Gone 'the program stops by itself'
    Assert-That (-not (Test-Path "$database-wal")) 'the WAL file is merged and gone'
    $logText = Get-Content (Get-ChildItem (Join-Path $dataFolder 'logs') -Filter *.log | Select-Object -First 1).FullName -Raw
    Assert-That ($logText -match 'asked to stop') 'the log shows that the program was asked to stop'
    Assert-That (Test-Path $database) 'the program made its database in the data folder'
    if ($python) {
        Invoke-Sql "INSERT INTO HousingUnits (Address, IsActive) VALUES ('Fiktivdalen 7C', 1)" | Out-Null
        Assert-That ((Invoke-Sql "SELECT COUNT(*) FROM HousingUnits WHERE Address = 'Fiktivdalen 7C'") -eq '1') 'a fictional housing unit is saved in the database'
    }

    if ($OldSetup) {
        Write-Host "4. Update while the program runs ($(Get-VersionOf $OldSetup) to $newVersion)"
        $exeBefore = (Get-FileHash (Join-Path $installDir 'SameieHub.exe')).Hash
        Set-Content (Join-Path $installDir 'stray-from-an-older-version.dll') 'old'
        $app = Start-Program
        Assert-That ($null -ne (Get-LoggedAddress)) 'the old version runs'
        Assert-That ((Invoke-Setup $Setup) -eq 0) 'the installer stops the program gently and ends with exit code 0'
        Assert-That ((Get-RunningProgram).Count -eq 0) 'the program is stopped'
        $logText = Get-Content (Get-ChildItem (Join-Path $dataFolder 'logs') -Filter *.log | Select-Object -First 1).FullName -Raw
        Assert-That (([regex]::Matches($logText, 'asked to stop')).Count -ge 2) 'the program was asked to stop, not killed'
        Assert-That ((Get-ItemProperty $uninstallKey).DisplayVersion -eq $newVersion) "the listed version is now $newVersion"
        Assert-That ((Get-FileHash (Join-Path $installDir 'SameieHub.exe')).Hash -ne $exeBefore) 'the program file was replaced'
        Assert-That (-not (Test-Path (Join-Path $installDir 'stray-from-an-older-version.dll'))) 'a file only the old version had was removed'
        Assert-That (Test-Path (Join-Path $installDir 'unins000.exe')) 'the uninstaller is still there'
        Assert-That ((Test-DataUnchanged)) 'the data folder is unchanged by the update'
        if ($python) { Assert-That ((Invoke-Sql "SELECT COUNT(*) FROM HousingUnits WHERE Address = 'Fiktivdalen 7C'") -eq '1') 'the fictional data is still in the database after the update' }
        $app = Start-Program
        Assert-That ($null -ne (Get-LoggedAddress)) 'the new version starts on the old database'
        Stop-ProgramGently | Out-Null
        if ($python) { Assert-That ((Invoke-Sql "SELECT COUNT(*) FROM HousingUnits WHERE Address = 'Fiktivdalen 7C'") -eq '1') 'the data is still there after the new version has run'; }
    }

    Write-Host '5. A program that cannot be stopped is never replaced'
    $exeBefore = (Get-FileHash (Join-Path $installDir 'SameieHub.exe')).Hash
    $holder = Start-Holder 'SameieHub.Running'
    $exit = Invoke-Setup $Setup
    Assert-That ($exit -ne 0) "the installer stops without installing when the program is in use and does not answer (exit code $exit)"
    Assert-That ((Get-FileHash (Join-Path $installDir 'SameieHub.exe')).Hash -eq $exeBefore) 'the program file was not touched'
    Stop-Process -Id $holder.Id -Force
    Wait-Until { -not [bool](Get-Process -Id $holder.Id -ErrorAction SilentlyContinue) } 10 | Out-Null

    Write-Host '6. The program does not start while the installer works'
    $holder = Start-Holder 'SameieHubSetup'
    $blocked = Start-Program
    Assert-That ($blocked.WaitForExit(30000) -and $blocked.ExitCode -eq 1) 'the program refuses to start while an installation is under way'
    Assert-That ((Get-RunningProgram).Count -eq 0) 'no program process was left'
    Stop-Process -Id $holder.Id -Force

    Write-Host '7. Uninstall'
    Assert-That (Invoke-Uninstall) 'the uninstaller removes the program and its entry'
    Assert-That (-not (Test-Path $shortcut)) 'the Start menu shortcut is gone'
    Assert-That (-not (Test-Path $uninstallKey)) 'the entry under Installed apps is gone'
    Assert-That (-not (Test-Path (Join-Path $installDir 'SameieHub.exe'))) 'the program file is gone'
    Assert-That (Test-Path $database) 'the database is still there'
    Assert-That (Test-Path $marker) 'the fictional marker in the real data folder is still there'
    Assert-That ((Test-DataUnchanged)) 'the data folder is unchanged by the uninstall'
    if ($python) { Assert-That ((Invoke-Sql "SELECT COUNT(*) FROM HousingUnits WHERE Address = 'Fiktivdalen 7C'") -eq '1') 'the fictional data is still in the database after the uninstall' }
    Assert-That (@(Get-ChildItem (Join-Path $dataFolder 'logs') -ErrorAction SilentlyContinue).Count -ge 1) 'the log files are still there'

    Write-Host '8. Installing again after the uninstall'
    Assert-That ((Invoke-Setup $Setup) -eq 0) 'the installer ends with exit code 0'
    $app = Start-Program
    Assert-That ($null -ne (Get-LoggedAddress)) 'the program starts again'
    Stop-ProgramGently | Out-Null
    if ($python) { Assert-That ((Invoke-Sql "SELECT COUNT(*) FROM HousingUnits WHERE Address = 'Fiktivdalen 7C'") -eq '1') 'the earlier data is still there after the new installation' }
    Assert-That (Invoke-Uninstall) 'the final uninstall works'
}
catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
    $failures.Add($_.Exception.Message)
}
finally {
    foreach ($holder in $holders) { Stop-Process -Id $holder.Id -Force -ErrorAction SilentlyContinue }
    Get-RunningProgram | Stop-Process -Force -ErrorAction SilentlyContinue
    # Leave the machine as it was: remove an installation this test made, and the fictional marker.
    if ((Test-Path (Join-Path $installDir 'unins000.exe')) -and (Test-Path $uninstallKey)) { Invoke-Uninstall | Out-Null }
    Remove-Item -LiteralPath $marker -Force -ErrorAction SilentlyContinue
    if (-not $realDataExisted -and (Test-Path $realData) -and @(Get-ChildItem $realData -Force).Count -eq 0) { Remove-Item -LiteralPath $realData -Force -ErrorAction SilentlyContinue }
    Start-Sleep -Milliseconds 500
    Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host ''
if ($failures.Count -gt 0) {
    Write-Host "$($failures.Count) of $checks checks failed." -ForegroundColor Red
    exit 1
}

Write-Host "All $checks checks passed."
exit 0
