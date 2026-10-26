#requires -Version 7
<#
.SYNOPSIS
    Generates a Kiota client project for each ChurchSuite API module.

.PARAMETER Modules
    Optional subset of module names (e.g. Calendar, SmallGroups). Defaults to all.

.PARAMETER SpecRoot
    Base location of the module OpenAPI specs. Defaults to the committed specs/ folder (see update-specs.ps1).
    Can be a URL or a folder; modules are always discovered from specs/*.yaml.
#>
[CmdletBinding()]
param(
    [string[]] $Modules,
    [string] $SpecRoot = "specs"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path $PSScriptRoot -Parent
$srcRoot = Join-Path $repoRoot "src"
$baseNamespace = "ChurchSuiteDotNet"

# Module names are discovered from specs/*.yaml. Title case is used unless overridden here.
$nameOverrides = @{
    smallgroups = "SmallGroups"
}
$allModules = [ordered]@{}
foreach ($file in Get-ChildItem (Join-Path $repoRoot "specs") -Filter *.yaml | Sort-Object Name) {
    $key = $file.BaseName
    $pascal = if ($nameOverrides.ContainsKey($key)) { $nameOverrides[$key] } else { (Get-Culture).TextInfo.ToTitleCase($key) }
    $allModules[$pascal] = $key
}

$selected = if ($Modules) { $Modules } else { $allModules.Keys }

Push-Location $repoRoot
try {
    dotnet tool restore | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "dotnet tool restore failed" }

    foreach ($module in $selected) {
        if (-not $allModules.Contains($module)) {
            throw "Unknown module '$module'. Known modules: $($allModules.Keys -join ', ')"
        }

        $projectName = "$baseNamespace.$module"
        $projectDir = Join-Path $srcRoot $projectName
        $generatedDir = Join-Path $projectDir "Generated"
        $spec = "$SpecRoot/$($allModules[$module]).yaml"
        New-Item -ItemType Directory -Force -Path $projectDir | Out-Null

        Write-Host "Generating $module from $spec"
        dotnet tool run kiota generate `
            --language csharp `
            --namespace-name $projectName `
            --class-name "${module}Client" `
            --openapi $spec `
            --output $generatedDir `
            --clean-output `
            --exclude-backward-compatible
        if ($LASTEXITCODE -ne 0) { throw "Kiota generation failed for $module using $spec" }

        # An info.version bump alone changes the lock file's hash; don't treat that as a client change.
        $lockFile = "src/$projectName/Generated/kiota-lock.json"
        $dirty = @(git status --porcelain --untracked-files=all -- "src/$projectName/Generated")
        if ($dirty.Count -eq 1 -and $dirty[0] -match '^ ?M ' -and $dirty[0].Substring(3).Trim('"') -eq $lockFile) {
            git checkout -- $lockFile
        }

        # Only written for a new module. Extend its Description to say what the module covers, like the others.
        $csproj = Join-Path $projectDir "$projectName.csproj"
        if (-not (Test-Path $csproj)) {
            @"
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <Description>Community-maintained .NET client for the ChurchSuite $module API.</Description>
    <PackageTags>`$(PackageTags);$($allModules[$module])</PackageTags>
    <NoWarn>`$(NoWarn);CS1591;CS8618</NoWarn>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\$baseNamespace\$baseNamespace.csproj" />
  </ItemGroup>

</Project>
"@ | Set-Content $csproj -Encoding utf8
            dotnet sln ChurchSuiteDotNet.slnx add $csproj
            if ($LASTEXITCODE -ne 0) { throw "Failed to add $csproj to the solution" }
        }

        $factoryExtensions = Join-Path $projectDir "ChurchSuiteClientFactoryExtensions.cs"
        if (-not (Test-Path $factoryExtensions)) {
            @"
namespace $projectName;

/// <summary>Creates the $module client from a <see cref="ChurchSuiteClientFactory"/>.</summary>
public static class ChurchSuiteClientFactoryExtensions
{
    public static ${module}Client Create${module}Client(this ChurchSuiteClientFactory factory) =>
        factory.Create(adapter => new ${module}Client(adapter));
}
"@ | Set-Content $factoryExtensions -Encoding utf8
        }
    }
}
finally {
    Pop-Location
}
