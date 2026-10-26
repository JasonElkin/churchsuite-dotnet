#requires -Version 7
<#
.SYNOPSIS
    Plans the next release. Previews by default; the Release workflow does the publishing (tag, pack, NuGet push,
    GitHub release).

    All packages are versioned in lockstep and released together from one tag: v0.4.0 for a stable release,
    v0.4.0-beta.1 for a pre-release. The first stable release is 0.1.0, or 1.0.0 with -Bump major.

.PARAMETER Bump
    Which part of the last stable version to increment (default: minor). Taken literally: major from 0.x, or
    before any stable release, goes to 1.0.0 (-Prerelease rc gives 1.0.0-rc.1). (The Update clients PR suggests minor for breaking changes in generated code, as the versioning policy
    in CONTRIBUTING.md allows, and for any breaking change while 0.x.)
    Pending pre-releases count: with v0.3.0-beta.2 out, minor or patch from v0.2.0 releases 0.3.0.

.PARAMETER Prerelease
    Release a pre-release on this channel (alpha, beta or rc) instead of a stable version, numbered after any
    earlier ones for the same version (v0.3.0-beta.1, v0.3.0-beta.2, v0.3.0-rc.1, ...).

.PARAMETER Breaking
    Projects whose generated API has breaking changes (comma separated or as an array), listed under a flagged
    section of the release notes.

.PARAMETER DetailsUrl
    Where the breaking changes are listed in full (the update PR), linked from that section.

.PARAMETER Json
    Print the plan as a JSON object (Tag, Version, Prerelease, LastTag, Notes) instead of a preview. Prints
    nothing if there is nothing to release.
#>
[CmdletBinding()]
param(
    [ValidateSet("major", "minor", "patch")] [string] $Bump = "minor",
    [ValidateSet("", "alpha", "beta", "rc")] [string] $Prerelease = "",
    [string[]] $Breaking = @(),
    [string] $DetailsUrl = "",
    [switch] $Json
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path $PSScriptRoot -Parent
Push-Location $repoRoot
try {
    . "$PSScriptRoot/versioning.ps1"

    # Anything that ends up in a package.
    $releasePaths = @("src", "specs", "Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props", "README.md")

    $tags = Get-ReleaseTags
    $lastStable = ($tags | Where-Object { -not $_.Channel } | Select-Object -Last 1).Tag
    $version = Get-NextVersion $Bump $Prerelease

    # Nothing to release if nothing changed since the last stable release, or if HEAD already has this pre-release.
    $sinceStable = @(git log $(if ($lastStable) { "$lastStable..HEAD" } else { "HEAD" }) --oneline -- @releasePaths)
    $channelPrefix = "v$($version -replace '\.\d+$', '')."
    $alreadyTagged = if ($Prerelease) { git tag --points-at HEAD | Where-Object { $_.StartsWith($channelPrefix) } | Select-Object -First 1 }
    if (-not $sinceStable -or $alreadyTagged) {
        if (-not $Json) { Write-Host "Nothing to release$(if ($alreadyTagged) { ": HEAD is already $alreadyTagged" } else { " since $lastStable" })." }
        return
    }

    # Stable notes cover everything since the last stable release; pre-release notes, since the previous release.
    $lastTag = if ($Prerelease) { ($tags | Select-Object -Last 1).Tag } else { $lastStable }
    $range = if ($lastTag) { "$lastTag..HEAD" } else { "HEAD" }
    $commits = @(git log $range --pretty="format:%s (%h)" -- @releasePaths)
    $packages = @(Get-ChildItem src -Directory | ForEach-Object Name | Sort-Object)
    $changedPackages = if ($lastTag) {
        @($packages | Where-Object { git diff --name-only $lastTag HEAD -- "src/$_" })
    }
    else { $packages }

    $newSpec = git show "HEAD:specs/VERSION" 2>$null
    $oldSpec = if ($lastTag) { git show "${lastTag}:specs/VERSION" 2>$null }
    $specLine = if ($newSpec -and $oldSpec -and $newSpec -ne $oldSpec) { "Generated from ChurchSuite API spec ``$oldSpec`` → ``$newSpec``." }
                elseif ($newSpec) { "Generated from ChurchSuite API spec ``$newSpec``." }

    $since = if ($lastTag) { "since ``$lastTag``" } else { "(first release)" }
    $changed = if ($changedPackages) { ($changedPackages | ForEach-Object { "- ``$_``" }) -join "`n" }
               elseif ($commits) { "_None (shared build files only)._" }
               else { "_None; same code as ``$lastTag``._" }
    $unchanged = ($packages | Where-Object { $_ -notin $changedPackages } | ForEach-Object { "``$_``" }) -join ", "
    $unchangedLine = if ($unchanged) { "`n`nRe-released unchanged: $unchanged." } else { "" }

    $breakingProjects = @($Breaking | ForEach-Object { $_ -split ',' } | ForEach-Object Trim | Where-Object { $_ })
    $breakingSection = if ($breakingProjects) {
        $see = if ($DetailsUrl) { " The changed types and members are listed in $DetailsUrl." } else { "" }
        "### Breaking changes in generated code`n`n" +
        "ChurchSuite's OpenAPI spec changed in ways that break the generated clients of these packages, so code that names a changed type or member may need updating.$see " +
        "As the [versioning policy](https://github.com/JasonElkin/churchsuite-dotnet#versioning) explains, these changes ship in minor releases.`n`n" +
        (($breakingProjects | ForEach-Object { "- ``$_``" }) -join "`n") + "`n`n"
    }
    else { "" }

    $install = if ($Prerelease) { "This is a pre-release. " } else { "" }
    $commitList = if ($commits) { ($commits | ForEach-Object { "- $_" }) -join "`n" } else { "_No new commits; same code as ``$lastTag``._" }

    $notes = @"
$($install)All ChurchSuiteDotNet packages are released together at $version, e.g. ``dotnet add package ChurchSuiteDotNet.Calendar --version $version``.
$(if ($specLine) { "`n$specLine`n" })
$($breakingSection)### Packages with changes $since
$changed$unchangedLine

### Commits
$commitList
"@

    $plan = [pscustomobject]@{ Tag = "v$version"; Version = $version; Prerelease = [bool]$Prerelease; LastTag = $lastTag; Notes = $notes }

    if ($Json) {
        ConvertTo-Json -InputObject $plan -Compress
    }
    else {
        Write-Host "=== $($plan.Tag) ===`n$notes`n"
        Write-Host "Preview only. The Release workflow publishes (merge an update PR, or run it manually)."
    }
}
finally {
    Pop-Location
}
