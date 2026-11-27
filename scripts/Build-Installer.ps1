<#
.SYNOPSIS
Builds the SameieHub installer (Inno Setup): publishes the program, compiles installer\SameieHub.iss, and writes a .sha256 file next to
the installer. The installer is for internal testing. It is not signed and is not a release.

.EXAMPLE
.\scripts\Build-Installer.ps1 -Version 0.8.4-test.1
.\scripts\Build-Installer.ps1 -Version 0.8.4-test.1 -SourceDir .\artifacts\windows\SameieHub-0.8.4-test.1-win-x64-test   # reuse a published folder

Needs Inno Setup 6 (ISCC.exe): winget install JRSoftware.InnoSetup  (or choco install innosetup). A path can be given with -Iscc or $env:ISCC.
#>
param(
    [Parameter(Mandatory)][string]$Version,
    [string]$SourceDir = '',
    [string]$OutputDir = '',
    [string]$Iscc = ''
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
if (-not $OutputDir) { $OutputDir = Join-Path $root 'artifacts\installer' }

if ($Version -notmatch '^\d+\.\d+\.\d+([-.][0-9A-Za-z.-]+)?$') { throw "The version '$Version' is not like 1.2.3 or 1.2.3-test.1." }
$numeric = ($Version -split '[-+]')[0] + '.0'

if (-not $Iscc) { $Iscc = $env:ISCC }
if (-not $Iscc) {
    $candidates = @(
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe")
    $Iscc = $candidates | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
    if (-not $Iscc) { $found = Get-Command ISCC.exe -ErrorAction SilentlyContinue; if ($found) { $Iscc = $found.Source } }
}
if (-not $Iscc -or -not (Test-Path $Iscc)) { throw 'Inno Setup (ISCC.exe) was not found. Install it with: winget install JRSoftware.InnoSetup' }

if (-not $SourceDir) {
    # The installed program is the normal build: without the test hooks.
    $SourceDir = & (Join-Path $PSScriptRoot 'Publish-Windows.ps1') -Version $Version -NoZip | Select-Object -Last 1
}
if (-not (Test-Path (Join-Path $SourceDir 'SameieHub.exe'))) { throw "SameieHub.exe was not found in $SourceDir." }

New-Item -ItemType Directory -Force $OutputDir | Out-Null
$setupName = "SameieHub-$Version-win-x64-Setup.exe"
$setup = Join-Path $OutputDir $setupName
if (Test-Path $setup) { Remove-Item -LiteralPath $setup -Force }

& $Iscc "/DVersion=$Version" "/DNumericVersion=$numeric" "/DSourceDir=$((Resolve-Path $SourceDir).Path)" "/DOutputDir=$((Resolve-Path $OutputDir).Path)" /Q (Join-Path $root 'installer\SameieHub.iss') | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup failed.' }
if (-not (Test-Path $setup)) { throw "The installer $setupName was not produced." }

$hash = (Get-FileHash $setup -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -Path "$setup.sha256" -Value "$hash *$setupName" -Encoding ascii
Write-Host ("Installer: {0}  ({1:N1} MB)" -f $setup, ((Get-Item $setup).Length / 1MB))
Write-Host "SHA-256:   $hash"

# The only output is the path of the installer, so another script can use it.
$setup
