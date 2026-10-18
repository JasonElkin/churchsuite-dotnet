#requires -Version 7
<#
.SYNOPSIS
    Pins specs/ to a tagged release of the upstream ChurchSuite OpenAPI specs.

.PARAMETER Tag
    Upstream tag to pin. Defaults to the latest version tag.
#>
[CmdletBinding()]
param(
    [string] $Tag
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path $PSScriptRoot -Parent
$specsDir = Join-Path $repoRoot "specs"
$upstream = "https://github.com/ChurchSuite/churchsuite-api"
$apiRoot = "https://api.github.com/repos/ChurchSuite/churchsuite-api"

if (-not $Tag) {
    $tags = git ls-remote --tags --refs $upstream |
        ForEach-Object { ($_ -split "`t")[1] -replace '^refs/tags/', '' } |
        Where-Object { $_ -match '^v?\d+(\.\d+)*$' }
    if (-not $tags) { throw "No version tags found in $upstream" }
    $Tag = $tags | Sort-Object { [version]($_ -replace '^v', '') } | Select-Object -Last 1
}

$headers = @{ "User-Agent" = "churchsuite-dotnet" }
if ($env:GITHUB_TOKEN) { $headers.Authorization = "Bearer $env:GITHUB_TOKEN" }

$files = @(Invoke-RestMethod -Headers $headers "$apiRoot/contents/src?ref=$Tag") |
    ForEach-Object { $_ } |
    Where-Object { $_.name -like "*.yaml" -and $_.name -ne "openapi.yaml" }
if (-not $files) { throw "No spec files found in $upstream at $Tag" }

$oldVersion = if (Test-Path "$specsDir/VERSION") { (Get-Content "$specsDir/VERSION" -Raw).Trim() } else { "" }

New-Item -ItemType Directory -Force -Path $specsDir | Out-Null
Get-ChildItem $specsDir -Filter *.yaml | Remove-Item
foreach ($file in $files) {
    Invoke-WebRequest -Headers $headers $file.download_url -OutFile (Join-Path $specsDir $file.name)
}
Set-Content "$specsDir/VERSION" $Tag -NoNewline

Write-Host "Upstream specs: '$oldVersion' -> '$Tag' ($($files.Count) files)"
if ($env:GITHUB_OUTPUT) {
    "old-version=$oldVersion" | Add-Content $env:GITHUB_OUTPUT
    "new-version=$Tag" | Add-Content $env:GITHUB_OUTPUT
}
