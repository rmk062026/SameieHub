#requires -Version 7.0
<#
.SYNOPSIS
Builds the files of a SameieHub release candidate, checks them, and writes the release notes. Nothing is published: no Git tag is made,
no GitHub Release is created, and nothing is signed. Publishing is a separate, manual step after approval (docs/RELEASE_PROCESS.md).

Output in artifacts\release\<version>\ :
  SameieHub-<version>-win-x64-Setup.exe   the installer (Inno Setup)
  SameieHub-<version>-win-x64.zip         the program folder as a zip (no installation)
  SHA256SUMS.txt                          SHA-256 of both files
  RELEASE-NOTES.md                        a draft of the notes, from the commit messages since the previous tag

.EXAMPLE
.\scripts\Build-Release.ps1                       # version from the Git tags (Get-Version.ps1)
.\scripts\Build-Release.ps1 -Version 1.0.0-rc.1
#>
param(
    [string]$Version = '',
    [string]$OutputRoot = ''
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not $Version) { $Version = & (Join-Path $PSScriptRoot 'Get-Version.ps1') }
if ($Version -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$') { throw "The version '$Version' is not like 1.2.3 or 1.2.3-rc.1." }
if (-not $OutputRoot) { $OutputRoot = Join-Path $root 'artifacts\release' }
$out = Join-Path $OutputRoot $Version
if (Test-Path $out) { Remove-Item -LiteralPath $out -Recurse -Force }
New-Item -ItemType Directory -Force $out | Out-Null

Write-Host "Release candidate $Version"
$dirty = git -C $root status --porcelain
if ($dirty) { Write-Host 'Note: the working tree has uncommitted changes, so this build cannot be reproduced from a commit.' -ForegroundColor Yellow }

# The normal build only: a release can never be made with the test hooks (Publish-Windows.ps1 refuses that).
$work = Join-Path $out 'work'
$zipPath = & (Join-Path $PSScriptRoot 'Publish-Windows.ps1') -Version $Version -Release -OutputRoot $work | Select-Object -Last 1
$folder = Join-Path $work "SameieHub-$Version-win-x64"
$setupPath = & (Join-Path $PSScriptRoot 'Build-Installer.ps1') -Version $Version -SourceDir $folder -OutputDir $out | Select-Object -Last 1

$zipName = "SameieHub-$Version-win-x64.zip"
Move-Item -LiteralPath $zipPath -Destination (Join-Path $out $zipName)
$setupName = Split-Path $setupPath -Leaf
Remove-Item "$setupPath.sha256" -Force -ErrorAction SilentlyContinue

# ---- checks on what will be handed out
$problems = New-Object System.Collections.Generic.List[string]
$info = (Get-Item (Join-Path $folder 'SameieHub.exe')).VersionInfo
if ($info.ProductVersion -notlike "$Version*") { $problems.Add("SameieHub.exe says version '$($info.ProductVersion)', not $Version.") }
$unwanted = @(Get-ChildItem $folder -Recurse -File | Where-Object { $_.Extension -in '.db', '.sqlite', '.log', '.pdb', '.pfx', '.key', '.env', '.sha256' -or $_.Name -match '^(secrets?|appsettings\.(secrets?|Development))\.json$' })
if ($unwanted.Count -gt 0) { $problems.Add('Files that must not be shipped: ' + ($unwanted.Name -join ', ')) }
$settings = Get-Content (Join-Path $folder 'appsettings.json') -Raw
if ($settings -match '(?i)password|secret|apikey|connectionstring') { $problems.Add('appsettings.json contains something that looks like a secret.') }
# The markers are attribute values in the assembly. They are looked for in the file's bytes, because this script may run in a
# PowerShell that cannot load a .NET 10 assembly.
$dllText = [Text.Encoding]::GetEncoding(28591).GetString([IO.File]::ReadAllBytes((Join-Path $folder 'SameieHub.dll')))
if ($dllText -notmatch 'SameieHub\.DefaultHostMode[\x00-\x20]{1,2}Windows') { $problems.Add('The program is not marked as the published Windows build.') }
if ($dllText -match 'SameieHub\.TestHooks') { $problems.Add('The program has the test hooks. It must never be released.') }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead((Join-Path $out $zipName))
$badInZip = @($archive.Entries | Where-Object { $_.FullName -match '\.(db|sqlite|log|pdb|pfx|key|env)$|(^|[/\\])(secrets?|appsettings\.Development)\.json$' } | ForEach-Object { $_.FullName })
$zipHasExe = @($archive.Entries | Where-Object { $_.FullName.Replace('\','/') -match '^([^/]+/)?SameieHub\.exe$' }).Count -eq 1
$archive.Dispose()
if ($badInZip.Count -gt 0) { $problems.Add('The zip contains: ' + ($badInZip -join ', ')) }
if (-not $zipHasExe) { $problems.Add('The zip does not have SameieHub.exe.') }
$setupInfo = (Get-Item (Join-Path $out $setupName)).VersionInfo
$numeric = ($Version -split '-')[0] + '.0'
$setupVersion = "$($setupInfo.FileMajorPart).$($setupInfo.FileMinorPart).$($setupInfo.FileBuildPart).$($setupInfo.FilePrivatePart)"
if ($setupVersion -ne $numeric) { $problems.Add("The installer's file version is $setupVersion, expected $numeric.") }

# ---- checksums
$sums = foreach ($name in $setupName, $zipName) {
    '{0} *{1}' -f (Get-FileHash (Join-Path $out $name) -Algorithm SHA256).Hash.ToLowerInvariant(), $name
}
Set-Content (Join-Path $out 'SHA256SUMS.txt') $sums -Encoding ascii

# ---- release notes (a draft: the commit messages are written for developers)
$ErrorActionPreference = 'Continue'   # Windows PowerShell 5 treats git's message about a missing tag as an error
$previous = git -C $root describe --tags --abbrev=0 --match 'v[0-9]*' 2>$null
$ErrorActionPreference = 'Stop'
$hasPrevious = ($LASTEXITCODE -eq 0) -and $previous
$range = if ($hasPrevious) { "$previous..HEAD" } else { 'HEAD' }
$subjects = @(git -C $root log $range --no-merges --pretty=format:%s)
$groups = [ordered]@{ feat = 'Nytt'; fix = 'Rettelser'; refactor = 'Intern opprydding'; docs = 'Dokumentasjon'; test = 'Tester'; ci = 'Bygging og CI'; chore = 'Annet' }
$notes = New-Object System.Collections.Generic.List[string]
$notes.Add("# SameieHub $Version")
$notes.Add('')
$notes.Add('**Utkast til utgivelsesnotater.** Gå gjennom dem og skriv dem om for lesere som ikke er utviklere før de publiseres.')
$notes.Add('')
$notes.Add('## Før du installerer')
$notes.Add('- Installasjonsfilen er **ikke signert**. Windows (SmartScreen) kan advare om ukjent utgiver. Kontroller SHA-256 under mot filen du lastet ned, og last bare ned fra stedet du fikk denne utgivelsen. Se `docs/BRUKERVEILEDNING.md`.')
$notes.Add('- Databasen er ikke kryptert. Lag sikkerhetskopier, og oppbevar dem trygt.')
$notes.Add('- Status og kjente begrensninger: `docs/RELEASE_CHECKLIST.md`.')
$notes.Add('')
$notes.Add('## Filer og kontrollsummer (SHA-256)')
$notes.Add('```')
foreach ($line in $sums) { $notes.Add($line) }
$notes.Add('```')
$notes.Add('')
$notes.Add($(if ($hasPrevious) { "## Endringer siden $previous" } else { '## Endringer' }))
foreach ($type in $groups.Keys) {
    $items = @($subjects | Where-Object { $_ -match "^$type(\(.+\))?!?: " } | ForEach-Object { $_ -replace "^$type(\(.+\))?!?: ", '' })
    if ($items.Count -gt 0) {
        $notes.Add('')
        $notes.Add("### $($groups[$type])")
        foreach ($item in $items) { $notes.Add("- $item") }
    }
}
Set-Content (Join-Path $out 'RELEASE-NOTES.md') $notes -Encoding utf8

Remove-Item -LiteralPath $work -Recurse -Force

Write-Host ''
Get-ChildItem $out | ForEach-Object { Write-Host ('  {0}  ({1:N1} MB)' -f $_.Name, ($_.Length / 1MB)) }
if ($problems.Count -gt 0) {
    Write-Host ''
    $problems | ForEach-Object { Write-Host "PROBLEM: $_" -ForegroundColor Red }
    throw "$($problems.Count) problem(s) found in the release files."
}

Write-Host 'The release files passed the checks. Nothing has been tagged, signed or published.'
$out
