<#
.SYNOPSIS
Gives the version of the current commit from the Git tags. Tags look like v1.2.3 (or v1.2.3-rc.1).
  - On a tagged commit:           1.2.3
  - After a tag (N commits on):   1.2.4-dev.N   (the next patch version, so it sorts after the tag)
  - No tag in the repository:     0.0.0-dev.N   (N is the number of commits)
The version is only text for the file names and the program's version information. Nothing is tagged or pushed by this script.

.EXAMPLE
.\scripts\Get-Version.ps1
.\scripts\Get-Version.ps1 -Tag v1.0.0     # checks the tag text and gives 1.0.0 (what a tag build in CI does)
#>
param([string]$Tag = '')

$ErrorActionPreference = 'Stop'
$pattern = '^v(\d+)\.(\d+)\.(\d+)(-[0-9A-Za-z.-]+)?$'

if ($Tag) {
    if ($Tag -notmatch $pattern) { throw "The tag '$Tag' is not like v1.2.3 or v1.2.3-rc.1." }
    return $Tag.Substring(1)
}

$ErrorActionPreference = 'Continue'   # Windows PowerShell 5 treats git's message about a missing tag as an error
$exact = git describe --tags --exact-match --match 'v[0-9]*' 2>$null
if ($LASTEXITCODE -eq 0 -and $exact -match $pattern) { return $exact.Substring(1) }

$described = git describe --tags --long --match 'v[0-9]*' 2>$null
if ($LASTEXITCODE -eq 0 -and $described -match '^v(\d+)\.(\d+)\.(\d+)(?:-[0-9A-Za-z.]+?)?-(\d+)-g[0-9a-f]+$') {
    return "$($Matches[1]).$($Matches[2]).$([int]$Matches[3] + 1)-dev.$($Matches[4])"
}

$ErrorActionPreference = 'Stop'
$count = git rev-list --count HEAD
return "0.0.0-dev.$count"
