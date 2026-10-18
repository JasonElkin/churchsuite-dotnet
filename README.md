# ChurchSuiteDotNet

A general-purpose .NET client for the [ChurchSuite v2 API](https://github.com/ChurchSuite/churchsuite-api). Each API module is generated with [Kiota](https://learn.microsoft.com/openapi/kiota/) and shipped as its own NuGet package.

| Package | Purpose |
|---|---|
| `ChurchSuiteDotNet` | Authentication (client credentials), options, `ChurchSuiteClientFactory`. No DI required. |
| `ChurchSuiteDotNet.Extensions.DependencyInjection` | `IServiceCollection` registration with `IHttpClientFactory` and resilience. |
| `ChurchSuiteDotNet.{Account, Addressbook, Attendance, Bookings, Calendar, Children, Giving, Network, Planning, Rotas, SmallGroups}` | Generated module clients. |

Targets .NET 10. Each new .NET major release is added as a target going forward; older majors are kept once added.

## Usage without dependency injection

```csharp
using ChurchSuiteDotNet;
using ChurchSuiteDotNet.Calendar;

using var factory = new ChurchSuiteClientFactory(new ChurchSuiteOptions
{
    ClientId = "...",
    ClientSecret = "...",
});

var calendar = factory.CreateCalendarClient();
```

## Usage with dependency injection (ASP.NET Core, worker services, etc.)

```csharp
// appsettings.json: { "ChurchSuite": { "ClientId": "...", "ClientSecret": "..." } }
services.AddChurchSuite(configuration.GetSection("ChurchSuite"));
services.AddChurchSuiteClient(adapter => new CalendarClient(adapter));
```

`AddChurchSuite` returns the `IHttpClientBuilder` for the API client so you can add handlers or tune resilience. `ChurchSuiteOptions` also exposes `BaseUrl`, `TokenUrl` and `Scope`.

## Development

- **Specs are committed.** `./scripts/update-specs.ps1 [-Tag 2.112.3]` pins `specs/` to a tagged release of the upstream [churchsuite-api](https://github.com/ChurchSuite/churchsuite-api) (the tag is recorded in `specs/VERSION`). `./scripts/generate-clients.ps1 [-Modules Calendar,Giving]` regenerates the clients from `specs/` (Kiota is pinned in `.config/dotnet-tools.json`), works offline, and discovers modules from `specs/*.yaml`. A new spec gets its project scaffolded and added to the solution automatically. Changes that only touch `kiota-lock.json` (e.g. an `info.version` bump) are discarded.
- **Update clients** runs weekly: it pins the latest upstream tag and regenerates the clients. If any module's generated code changed, it opens a PR (label `api-update`) with the upstream CHANGELOG entries, a table classifying each module's change as breaking, additive or docs-only (SDK package validation against the last release), and a `release-plan` block with one suggested `bump:`. Change or delete that line before merging; re-runs rewrite the PR body, so do it just before merging. Add a `PR_TOKEN` secret (preferably a GitHub App token) so CI runs on that PR.
- **All packages share one version** (lockstep), taken from `v<version>` tags via [MinVer](https://github.com/adamralph/minver), e.g. `v0.4.0`. Every release publishes every package, and modules depend on the core package of the same release. The first release is `0.1.0`. While at 0.x a breaking change is suggested as a minor bump; run a manual `major` release to move to 1.0.0.
- **Merging the PR publishes.** The **Release** workflow reads the bump from the `release-plan` block, tests, tags, packs every package, pushes them to NuGet (core first) using [trusted publishing](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing) (no API key; set the `NUGET_USER` secret to your nuget.org profile name and add a policy for this repo and `release.yml`), and creates one GitHub release with all the packages attached. The tag is pushed only after NuGet accepts every package, so a failed run can be re-run.
- **Manual releases** (core/DI changes, one-offs): run **Release** from the Actions tab with a `bump`, and optionally `dry_run` to pack without publishing. It does nothing if nothing has changed since the last stable release.
- **Pre-releases**: run **Release** manually with `prerelease` set to `alpha`, `beta` or `rc`. The version is the bump of the last stable release plus a numbered suffix, e.g. `v0.3.0-beta.1`, then `v0.3.0-beta.2`, `v0.3.0-rc.1`; channels only move forward. They're marked as pre-releases on GitHub and NuGet (install with `--prerelease` or the exact version). A later stable release of any bump that isn't larger publishes that same version (`v0.3.0`), and its notes cover everything since the last stable release. Merged update PRs always publish a stable release, and change suggestions compare against the last stable release.
- Preview the next release and its notes locally with `./scripts/tag-release.ps1 [-Bump minor] [-Prerelease beta]`.
