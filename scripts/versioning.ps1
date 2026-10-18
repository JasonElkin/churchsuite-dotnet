#requires -Version 7
# Shared version helpers for the release scripts; dot-source this file from the repo root.
# Release tags: v<major>.<minor>.<patch> (stable) and v<major>.<minor>.<patch>-<alpha|beta|rc>.<n> (pre-release).

$PrereleaseChannels = @("alpha", "beta", "rc")

# All release tags, lowest version first. Channel is "" for stable releases.
function Get-ReleaseTags {
    @(git tag --list "v[0-9]*" | ForEach-Object {
            if ($_ -match '^v(?<base>\d+\.\d+\.\d+)(-(?<channel>alpha|beta|rc)\.(?<n>\d+))?$') {
                [pscustomobject]@{
                    Tag     = $_
                    Base    = [version]$Matches.base
                    Channel = [string]$Matches.channel
                    Number  = [int]$Matches.n
                }
            }
        } | Sort-Object Base, @{ Expression = { if ($_.Channel) { $PrereleaseChannels.IndexOf($_.Channel) } else { 99 } } }, Number)
}

function Get-LastStableTag {
    (Get-ReleaseTags | Where-Object { -not $_.Channel } | Select-Object -Last 1).Tag
}

# The next version for a bump of the last stable release, as a pre-release on the given channel if one is given.
# Pending pre-releases count: with v0.3.0-beta.2 out, a minor or patch bump from v0.2.0 gives 0.3.0 (or 0.3.0-rc.1).
function Get-NextVersion([string] $Bump, [string] $Prerelease) {
    $tags = Get-ReleaseTags
    $stable = $tags | Where-Object { -not $_.Channel } | Select-Object -Last 1

    $base = if ($stable) {
        $v = $stable.Base
        switch ($Bump) {
            "major" { [version]::new($v.Major + 1, 0, 0) }
            "minor" { [version]::new($v.Major, $v.Minor + 1, 0) }
            default { [version]::new($v.Major, $v.Minor, $v.Build + 1) }
        }
    }
    else { [version]"0.1.0" }

    $pending = $tags | Where-Object { $_.Channel -and (-not $stable -or $_.Base -gt $stable.Base) } | Select-Object -Last 1
    if ($pending -and $pending.Base -gt $base) { $base = $pending.Base }

    if (-not $Prerelease) { return "$base" }

    $same = @($tags | Where-Object { $_.Channel -and $_.Base -eq $base })
    $latest = $same | Select-Object -Last 1
    if ($latest -and $PrereleaseChannels.IndexOf($latest.Channel) -gt $PrereleaseChannels.IndexOf($Prerelease)) {
        throw "$base is already at $($latest.Tag); a $Prerelease pre-release would sort before it."
    }
    $n = 1 + (($same | Where-Object Channel -eq $Prerelease | Measure-Object Number -Maximum).Maximum ?? 0)
    "$base-$Prerelease.$n"
}
