<#
.SYNOPSIS
Smoke test of the internal Windows test package. It unpacks the zip to a temporary folder, starts SameieHub.exe the way a double-click
does (no flags, and with SAMEIEHUB_WINDOWS_HOST=0 set to try to weaken it) against a temporary database, checks that nothing can be reached without the session cookie, plays the browser's part
(the one-time code, the cookie, a second start), stops the program the way a person would, and checks that nothing is left behind.
The ordinary database in AppData is never used, and the test data is empty.

The zip must be the test-hooks build (Publish-Windows.ps1 -TestHooks): the checks need Launch:UrlFile to play the browser's part.

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
    $urlFile = Join-Path $work 'launch-url.txt'
    $env:Database__Path = $database
    $env:Launch__UrlFile = $urlFile
    # An attempt to switch the protected mode off. It must have no effect on the published program, so every check below also proves that.
    $env:SAMEIEHUB_WINDOWS_HOST = '0'
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
        Remove-Item Env:Launch__UrlFile
        Remove-Item Env:SAMEIEHUB_WINDOWS_HOST
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

    $client = New-Object System.Net.Http.HttpClient ((New-Object System.Net.Http.HttpClientHandler -Property @{ UseCookies = $false; AllowAutoRedirect = $false }))
    function Send-Request([string]$Method, [string]$Path, [string]$Cookie = '', [hashtable]$Headers = @{}, [string]$Body = $null) {
        $request = New-Object System.Net.Http.HttpRequestMessage ([System.Net.Http.HttpMethod]::new($Method)), ($address.TrimEnd('/') + $Path)
        if ($Cookie) { $request.Headers.Add('Cookie', $Cookie) }
        foreach ($name in $Headers.Keys) {
            if ($name -eq 'Host') { $request.Headers.Host = $Headers[$name] } else { $request.Headers.Add($name, $Headers[$name]) }
        }
        if ($null -ne $Body) { $request.Content = New-Object System.Net.Http.StringContent ($Body) }
        return $client.SendAsync($request).GetAwaiter().GetResult()
    }
    function Get-Code([string]$File) {
        $url = (Get-Content $File -Raw).Trim()
        return $url.Substring($url.IndexOf('#') + 1)
    }

    Write-Host 'Without a session'
    foreach ($path in '/', '/oppgaver', '/boenheter/ny', '/innstillinger/sikkerhetskopi', '/favicon.png', '/_blazor', '/nothing-here') {
        Assert-That ((Send-Request 'GET' $path).StatusCode -eq 403) "GET $path is refused (403)"
    }
    Assert-That ((Send-Request 'POST' '/_blazor/negotiate?negotiateVersion=1').StatusCode -eq 403) 'the Blazor connection (negotiate) is refused'
    Assert-That ((Send-Request 'POST' '/boenheter/ny' '' @{} 'x=y').StatusCode -eq 403) 'a form post is refused'
    Assert-That ((Send-Request 'GET' '/' '' @{ Host = 'evil.example' }).StatusCode -eq 400) 'a foreign host name is refused (400)'

    $start = Send-Request 'GET' '/start'
    $startHtml = $start.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    Assert-That ($start.StatusCode -eq 200) 'the start page answers 200'
    Assert-That ($start.Headers.GetValues('Referrer-Policy') -contains 'no-referrer') 'the start page sends Referrer-Policy: no-referrer'
    Assert-That ($start.Headers.CacheControl.ToString() -match 'no-store') 'the start page sends Cache-Control: no-store'
    Assert-That (($start.Headers.GetValues('Content-Security-Policy') -join ' ') -match "default-src 'none'") 'the start page has a content security policy'
    Assert-That ($startHtml -notmatch 'https?://') 'the start page loads nothing from outside'
    Assert-That ((Send-Request 'POST' '/start' '' @{} 'wrong-code').StatusCode -eq 403) 'a wrong code is refused'

    Write-Host 'The one-time code'
    Assert-That (Test-Path $urlFile) 'the program made a launch address for the browser'
    $firstCode = Get-Code $urlFile
    Assert-That ($firstCode.Length -ge 43) 'the code is long (at least 256 bits as text)'
    $redeem = Send-Request 'POST' '/start' '' @{} $firstCode
    Assert-That ($redeem.StatusCode -eq 204) 'the code is accepted once (204)'
    $setCookie = ($redeem.Headers.GetValues('Set-Cookie') | Select-Object -First 1)
    Assert-That ($setCookie -match '(?i)httponly') 'the cookie is HttpOnly'
    Assert-That ($setCookie -match '(?i)samesite=strict') 'the cookie is SameSite=Strict'
    Assert-That ($setCookie -notmatch '(?i)domain=|expires=|max-age=') 'the cookie has no domain and no expiry (it ends with the browser)'
    Assert-That ((Send-Request 'POST' '/start' '' @{} $firstCode).StatusCode -eq 403) 'the same code cannot be used twice'
    $cookie = ($setCookie -split ';')[0]
    Assert-That ($cookie -notmatch [regex]::Escape($firstCode)) 'the cookie does not contain the code'

    Write-Host 'With a session'
    $page = Send-Request 'GET' '/' $cookie
    $html = $page.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    Assert-That ($page.StatusCode -eq 200) 'the front page answers 200 with the cookie'
    Assert-That ($html -match 'SameieHub') 'the front page is SameieHub'
    foreach ($path in '/oppgaver', '/styresaker', '/boenheter', '/elbillading', '/innstillinger/sikkerhetskopi') {
        Assert-That ((Send-Request 'GET' $path $cookie).StatusCode -eq 200) "GET $path answers 200 with the cookie"
    }

    $assets = @([regex]::Matches($html, '(?:href|src)="(?<path>[^"]+\.(?:css|js))"') | ForEach-Object { $_.Groups['path'].Value } | Sort-Object -Unique)
    Assert-That ($assets.Count -ge 3) "the page links its style sheets and scripts ($($assets.Count) found)"
    foreach ($asset in $assets) {
        $path = '/' + $asset.TrimStart('/')
        Assert-That ((Send-Request 'GET' $path).StatusCode -eq 403) "static file $asset is refused without the cookie"
        Assert-That ((Send-Request 'GET' $path $cookie).StatusCode -eq 200) "static file $asset answers 200 with the cookie"
    }

    $negotiate = Send-Request 'POST' '/_blazor/negotiate?negotiateVersion=1' $cookie @{ Origin = $address }
    Assert-That ($negotiate.StatusCode -eq 200) 'the Blazor connection (negotiate) works with the cookie'
    Assert-That ((Send-Request 'POST' '/_blazor/negotiate?negotiateVersion=1' $cookie @{ Origin = 'https://evil.example' }).StatusCode -eq 403) 'a foreign origin is refused even with the cookie'
    Assert-That ((Send-Request 'GET' '/' $cookie @{ 'Sec-Fetch-Site' = 'cross-site' }).StatusCode -eq 403) 'a cross-site request is refused even with the cookie'
    Assert-That ((Send-Request 'GET' '/' $cookie @{ Host = 'evil.example' }).StatusCode -eq 400) 'a foreign host name is refused even with the cookie'

    Write-Host 'A second start'
    $before = (Get-Item $urlFile).LastWriteTimeUtc
    Start-Sleep -Milliseconds 1100
    $env:Database__Path = $database
    try {
        $second = Start-Process $program.FullName -ArgumentList '--unattended' -WorkingDirectory $work -PassThru -Wait
    }
    finally {
        Remove-Item Env:Database__Path
    }
    Assert-That ($second.ExitCode -eq 0) 'a second start asks the running program to open the browser and ends with exit code 0'
    Assert-That ((Get-RunningProgram $folder).Count -eq 1) 'still exactly one SameieHub process'
    Assert-That ((Get-Item $urlFile).LastWriteTimeUtc -gt $before) 'the running program made a new launch address'
    $secondCode = Get-Code $urlFile
    Assert-That ($secondCode -ne $firstCode) 'the new code is a different code'
    $redeemSecond = Send-Request 'POST' '/start' '' @{} $secondCode
    Assert-That ($redeemSecond.StatusCode -eq 204) 'the new code gives access'
    Assert-That ((Send-Request 'GET' '/' $cookie).StatusCode -eq 200) 'the first session is still valid'

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
    Assert-That (($logText -notmatch [regex]::Escape($firstCode)) -and ($logText -notmatch [regex]::Escape($secondCode))) 'the log contains no launch code'
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
