<#
.SYNOPSIS
Builds the internal Windows test package: a self-contained win-x64 publish of SameieHub (no .NET needed to run it), the read-me
and the start file, and a zip of the folder. Nothing is installed, and nothing is added to the repository (artifacts/ is ignored).

.EXAMPLE
.\scripts\Publish-Windows.ps1                       # version 0.8.2-test
.\scripts\Publish-Windows.ps1 -Version 0.8.2-ci.15  # CI gives the run number
#>
param(
    [string]$Version = '0.8.2-test',
    [string]$OutputRoot = ''
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
if (-not $OutputRoot) { $OutputRoot = Join-Path $root 'artifacts\windows' }
$project = Join-Path $root 'src\SameieHub.Web\SameieHub.Web.csproj'
$packageName = "SameieHub-$Version-win-x64-test"
$stage = Join-Path $OutputRoot $packageName
$zip = Join-Path $OutputRoot "$packageName.zip"

New-Item -ItemType Directory -Force $OutputRoot | Out-Null
# Only this package's own folder and zip are replaced.
if (Test-Path $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
if (Test-Path $zip) { Remove-Item -LiteralPath $zip -Force }

# Self-contained: the .NET runtime is part of the folder. No trimming and no single file (see docs/WINDOWS_DISTRIBUTION_PLAN.md).
# Without debug symbols the folder is smaller and contains no source paths.
dotnet publish $project -c Release -r win-x64 --self-contained true `
    -p:PublishTrimmed=false -p:PublishSingleFile=false -p:DebugType=none -p:DebugSymbols=false `
    -p:Version=$Version -o $stage | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }

# The development settings are only for the development environment, which the Windows program never uses.
$development = Join-Path $stage 'appsettings.Development.json'
if (Test-Path $development) { Remove-Item -LiteralPath $development -Force }

Copy-Item (Join-Path $root 'packaging\windows\*') $stage

if (-not (Test-Path (Join-Path $stage 'SameieHub.exe'))) { throw 'SameieHub.exe was not produced.' }

Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($stage, $zip, [System.IO.Compression.CompressionLevel]::Optimal, $true)

$folderBytes = (Get-ChildItem $stage -Recurse -File | Measure-Object Length -Sum).Sum
$fileCount = (Get-ChildItem $stage -Recurse -File).Count
Write-Host ("Folder: {0}  ({1:N1} MB, {2} files)" -f $stage, ($folderBytes / 1MB), $fileCount)
Write-Host ("Zip:    {0}  ({1:N1} MB)" -f $zip, ((Get-Item $zip).Length / 1MB))

# The path of the zip is the only output, so another script can use it.
$zip
