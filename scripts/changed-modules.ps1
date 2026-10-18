$ErrorActionPreference = "Stop"
$repoRoot = Split-Path $PSScriptRoot -Parent
Push-Location $repoRoot
try {
    $entries = @(git status --porcelain --untracked-files=all -- src |
        ForEach-Object { [pscustomobject]@{ Status = $_.Substring(0, 2).Trim(); Path = $_.Substring(3).Trim('"') } } |
        Where-Object { $_.Path -match '^src/([^/]+)/Generated/' -and $_.Path -notmatch '/kiota-lock\.json$' })

    $changed = @($entries | ForEach-Object { ($_.Path -split '/')[1] } | Sort-Object -Unique)
    # Modules with no tracked project yet; worth a look at the generated name before release.
    $new = @($changed | Where-Object { -not (git ls-files "src/$_/$_.csproj") })

    Write-Host "Changed modules: $($changed -join ', ')"
    if ($new) { Write-Host "New modules: $($new -join ', ')" }

    if ($env:GITHUB_OUTPUT) {
        "changed=$($changed -join ',')" | Add-Content $env:GITHUB_OUTPUT
        "new=$($new -join ',')" | Add-Content $env:GITHUB_OUTPUT
    }
}
finally {
    Pop-Location
}
