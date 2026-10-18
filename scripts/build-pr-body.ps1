#requires -Version 7
<#
.SYNOPSIS
    Builds the update PR body: upstream version range, upstream CHANGELOG entries, the change classification
    table and the editable release-plan block (one shared bump) that the Release workflow reads on merge.
#>
[CmdletBinding()]
param(
    [string] $OldVersion = "",
    [Parameter(Mandatory)] [string] $NewVersion,
    [Parameter(Mandatory)] [string] $Changed,
    [string] $New = "",
    [ValidateSet("major", "minor", "patch")] [string] $Bump = "minor",
    [string] $BumpsMarkdown = "artifacts/release-bumps.md",
    [Parameter(Mandatory)] [string] $OutPath
)

$ErrorActionPreference = "Stop"
$upstream = "https://github.com/ChurchSuite/churchsuite-api"

$changelog = ""
try {
    $raw = Invoke-RestMethod "https://raw.githubusercontent.com/ChurchSuite/churchsuite-api/$NewVersion/CHANGELOG.md"
    $lo = if ($OldVersion) { [version]($OldVersion -replace '^v', '') } else { [version]"0.0" }
    $hi = [version]($NewVersion -replace '^v', '')
    $sections = [regex]::Matches([string]$raw, '(?ms)^## \[(?<v>[^\]]+)\].*?(?=^## \[|\z)') |
        Where-Object { [version]::TryParse($_.Groups['v'].Value, [ref]$null) -and ([version]$_.Groups['v'].Value) -gt $lo -and ([version]$_.Groups['v'].Value) -le $hi } |
        ForEach-Object { $_.Value.TrimEnd() -replace '(?m)^#{2,3} ', '#### ' }
    $changelog = $sections -join "`n`n"
}
catch { Write-Warning "Could not read the upstream CHANGELOG: $_" }
if (-not $changelog) { $changelog = "_No CHANGELOG entries found._" }

# Preview of the stable version the suggested bump produces (tag-release.ps1 recalculates it on merge).
. "$PSScriptRoot/versioning.ps1"
$lastTag = Get-LastStableTag
$next = Get-NextVersion $Bump ""
$from = if ($lastTag) { " (from ``$lastTag``)" } else { " (first release)" }

$table = if (Test-Path $BumpsMarkdown) { (Get-Content $BumpsMarkdown) -join "`n" } else { "" }
$newNote = if ($New) { "`n`n**New modules** (check the generated names before merging): ``$New``" } else { "" }
$range = if ($OldVersion) { "``$OldVersion`` → ``$NewVersion`` ([compare]($upstream/compare/$OldVersion...$NewVersion), [CHANGELOG]($upstream/blob/$NewVersion/CHANGELOG.md))" }
         else { "``$NewVersion`` ([CHANGELOG]($upstream/blob/$NewVersion/CHANGELOG.md))" }

@"
Automated regeneration from the upstream ChurchSuite OpenAPI specs: $range.

Changed modules: ``$Changed``$newNote

**Merging publishes every package to NuGet as ``$next``**$from. All packages share one version, so unchanged ones are re-released too. The bump below is suggested from package validation against the last release (the largest change wins). Change it to ``major``, ``minor`` or ``patch``, or delete the line to merge without publishing. If this workflow runs again before merge it rewrites this body and your edit is lost.

$table

<!-- release-plan: change the bump, or delete the line to skip publishing -->
bump: $Bump
<!-- /release-plan -->

<details><summary>Upstream CHANGELOG</summary>

$changelog

</details>
"@ | Set-Content $OutPath -Encoding utf8
