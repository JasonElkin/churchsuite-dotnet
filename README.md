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

```json
// appsettings.json
{
  "ChurchSuite": {
    "ClientId": "...",
    "ClientSecret": "..."
  }
}
```

```csharp
// Program.cs or Startup.cs
services.AddChurchSuite(configuration.GetSection("ChurchSuite"));
services.AddChurchSuiteClient(adapter => new CalendarClient(adapter));
```

`AddChurchSuite` returns the `IHttpClientBuilder` for the API client so you can add handlers or tune resilience. `ChurchSuiteOptions` also exposes `BaseUrl`, `TokenUrl` and `Scope`.

## Development

### Regenerating the clients

The upstream [OpenAPI specs](https://github.com/ChurchSuite/churchsuite-api) are committed in `specs/`, pinned to a tagged upstream release (recorded in `specs/VERSION`).

```powershell
./scripts/update-specs.ps1 [-Tag 2.112.3]          # pin specs/ to an upstream tag (default: latest)
./scripts/generate-clients.ps1 [-Modules Calendar]  # regenerate clients from specs/ (works offline)
```

- Modules are discovered from `specs/*.yaml`. A new spec gets its project scaffolded and added to the solution.
- Kiota is pinned in `.config/dotnet-tools.json`.
- Changes that only touch `kiota-lock.json` (e.g. an upstream `info.version` bump) are discarded.
- CI regenerates the clients and fails if `Generated/` differs, so don't edit generated code by hand.

### Versioning

- All packages share **one version** (lockstep), taken from git tags by [MinVer](https://github.com/adamralph/minver): `v0.4.0` for a release, `v0.4.0-beta.1` for a pre-release.
- Every release publishes every package. Modules depend on the core package of the same release.
- The first release is `0.1.0`. While at 0.x, breaking changes are suggested as a minor bump; run a manual `major` release to move to 1.0.0.

### Releasing an API update

1. **Update clients** runs weekly (or manually from the Actions tab). It pins the latest upstream tag, regenerates, and, if any module's generated code changed, opens a PR labelled `api-update` containing:
   - the upstream CHANGELOG entries;
   - each module's change, classified as breaking, additive or docs-only (SDK package validation against the last stable release);
   - a `release-plan` block with one suggested bump, e.g. `bump: minor`.
2. **Review the PR.** Change the `bump:` line if you disagree, or delete it to merge without publishing. Re-runs rewrite the PR body, so edit it just before merging.
3. **Merge.** The **Release** workflow tests, tags, packs every package, pushes them to NuGet (core first) and creates one GitHub release with the packages attached. The tag is only pushed once NuGet has accepted every package, so a failed run can simply be re-run.

Merged update PRs always publish a stable release.

### Manual releases and pre-releases

Run **Release** from the Actions tab for core/DI changes, one-off releases or pre-releases:

| Input | Values | Effect |
|---|---|---|
| `bump` | `minor` (default), `patch`, `major` | Which part of the last stable version to increment. |
| `prerelease` | `none` (default), `alpha`, `beta`, `rc` | Publish a pre-release instead, e.g. `v0.3.0-beta.1`. |
| `dry_run` | `false` (default), `true` | Plan and pack only; publish nothing. |

It does nothing if nothing has changed since the last stable release.

Pre-releases:

- Are numbered per version and channel: `v0.3.0-beta.1`, `v0.3.0-beta.2`, `v0.3.0-rc.1`. Channels only move forward (alpha → beta → rc).
- Are marked as pre-releases on GitHub and NuGet; install with `--prerelease` or the exact version.
- Are followed by the stable release of the same version: once `v0.3.0-beta.2` is out, a `minor` or `patch` release publishes `v0.3.0`, with notes covering everything since the last stable release.

Preview the next release and its notes locally:

```powershell
./scripts/tag-release.ps1 [-Bump minor] [-Prerelease beta]
```

### One-time setup

| Secret | Purpose |
|---|---|
| `NUGET_USER` | Your nuget.org profile name, for [trusted publishing](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing) (no API key). Also add a trusted publishing policy on nuget.org for this repo and `release.yml`. |
| `PR_TOKEN` | A fine-grained PAT (Contents and Pull requests: read and write) or GitHub App token, so CI runs on the update PRs. Without it the PRs are still opened, but CI doesn't run on them. |
