#requires -Version 7
<#
.SYNOPSIS
    Classifies the change to each module since the last release as breaking, additive or docs-only, using SDK
    package validation (ApiCompat) against the last published package, and suggests one bump for the release.

    Packages are versioned in lockstep, so the suggested bump is the largest across modules. Breaking is major
    from 1.x upwards and minor while 0.x; additive (or a new module) is minor; docs-only is patch.

.PARAMETER Modules
    Project names (ChurchSuiteDotNet.Calendar) or prefixes (calendar), comma separated or as an array.

.PARAMETER MarkdownPath
    Where to write the Markdown table for the PR body.

.OUTPUTS
    Writes bump=<major|minor|patch> to $GITHUB_OUTPUT when running in Actions.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string[]] $Modules,
    [string] $MarkdownPath = "artifacts/release-bumps.md"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path $PSScriptRoot -Parent
Push-Location $repoRoot
try {
    # Pre-releases are ignored: the suggestion is for the next stable release.
    . "$PSScriptRoot/versioning.ps1"
    $lastTag = Get-LastStableTag
    $last = if ($lastTag) { $lastTag.Substring(1) } else { $null }

    # Returns the ApiCompat (CPxxxx) errors; throws if the pack fails for any other reason.
    function Get-CompatErrors([string] $path, [bool] $strict) {
        $props = @("-p:EnablePackageValidation=true", "-p:PackageValidationBaselineVersion=$last")
        if ($strict) { $props += "-p:EnableStrictModeForBaselineValidation=true" }
        # Validation is incremental and its inputs don't include strict mode, so force it to run every time.
        Get-ChildItem $path -Recurse -Filter "Microsoft.NET.ApiCompat.ValidatePackage.semaphore" | Remove-Item
        $out =Join-Path ([IO.Path]::GetTempPath()) "classify-$([guid]::NewGuid())"
        $log = @(dotnet pack $path -c Release -o $out --nologo @props 2>&1 | ForEach-Object { "$_" })
        if ($LASTEXITCODE -eq 0) { return @() }
        $errors = @($log | Where-Object { $_ -match 'error CP\d{4}' } | Sort-Object -Unique)
        if (-not $errors) { $log | Write-Host; throw "dotnet pack failed for $path (not an API compatibility error)" }
        $errors
    }

    $rank = @{ patch = 0; minor = 1; major = 2 }
    $bump = "patch"
    $rows = @()
    foreach ($m in ($Modules | ForEach-Object { $_ -split ',' } | Where-Object { $_ })) {
        $prefix = ($m -replace '^ChurchSuiteDotNet\.', '').ToLowerInvariant()
        $name = (Get-ChildItem src -Directory | Where-Object { $_.Name -ieq "ChurchSuiteDotNet.$prefix" }).Name
        if (-not $name) { throw "No project found for module '$m'" }

        if (-not $lastTag) {
            $kind = "new"; $suggested = "minor"
        }
        elseif (-not (git ls-tree --name-only $lastTag -- "src/$name/$name.csproj")) {
            $kind = "new module"; $suggested = "minor"
        }
        else {
            $errors = @(Get-CompatErrors "src/$name" $false)
            if ($errors) {
                $kind = "breaking"
                $suggested = if ($last.StartsWith("0.")) { "minor" } else { "major" }
            }
            else {
                $errors = @(Get-CompatErrors "src/$name" $true)
                if ($errors) { $kind = "additive"; $suggested = "minor" } else { $kind = "docs-only"; $suggested = "patch" }
            }
            if ($errors) { Write-Host "${name}: $kind`n$($errors -join "`n")" }
        }

        if ($rank[$suggested] -gt $rank[$bump]) { $bump = $suggested }
        $rows += "| $name | $kind | $suggested |"
    }

    $baseline = if ($lastTag) { "Compared with ``$lastTag``." } else { "No release yet; the first release is 0.1.0." }
    New-Item -ItemType Directory -Force -Path (Split-Path $MarkdownPath) | Out-Null
    @("| Module | Change | Suggests |", "|---|---|---|") + $rows + @("", $baseline) | Set-Content $MarkdownPath
    Get-Content $MarkdownPath
    Write-Host "Suggested bump: $bump"

    if ($env:GITHUB_OUTPUT) { "bump=$bump" | Add-Content $env:GITHUB_OUTPUT }
}
finally {
    Pop-Location
}
