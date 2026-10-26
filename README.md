# ChurchSuiteDotNet

[![NuGet](https://img.shields.io/nuget/v/ChurchSuiteDotNet?logo=nuget)](https://www.nuget.org/packages/ChurchSuiteDotNet)
[![NuGet downloads](https://img.shields.io/nuget/dt/ChurchSuiteDotNet?logo=nuget)](https://www.nuget.org/packages/ChurchSuiteDotNet)
[![CI](https://github.com/JasonElkin/churchsuite-dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/JasonElkin/churchsuite-dotnet/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/github/license/JasonElkin/churchsuite-dotnet)](LICENSE)

A .NET client for the [ChurchSuite v2 API](https://github.com/ChurchSuite/churchsuite-api).

- **Strongly typed:** clients for every ChurchSuite API module, generated from ChurchSuite's own OpenAPI specs.
- **Authentication handled for you:** give it a client ID and secret, and it gets and refreshes the access tokens.
- **Works anywhere:** use it in a console app or script, or register it with dependency injection in ASP.NET Core and worker services.
- **Kept up to date:** new API releases are picked up and published automatically.

> This is a community project. It isn't affiliated with or supported by ChurchSuite.

## Installation

Install the package for each ChurchSuite module you want to use. Each one brings in the core `ChurchSuiteDotNet` package.

```bash
dotnet add package ChurchSuiteDotNet.Calendar
```

Available modules: `Account`, `Addressbook`, `Attendance`, `Bookings`, `Calendar`, `Children`, `Giving`, `Network`, `Planning`, `Rotas` and `SmallGroups`, each published as `ChurchSuiteDotNet.<Module>`.

If you use dependency injection, also install:

```bash
dotnet add package ChurchSuiteDotNet.Extensions.DependencyInjection
```

Requires .NET 10 or later.

## Getting started

ChurchSuiteDotNet signs in with the OAuth2 client credentials grant, so you'll need an API-enabled ChurchSuite user and a secret created for it. ChurchSuite's [authorisation docs](https://developer.churchsuite.com/auth) walk through setting both up; the user's identifier is your client ID.

```csharp
using ChurchSuiteDotNet;
using ChurchSuiteDotNet.Calendar;

using var factory = new ChurchSuiteClientFactory(new ChurchSuiteOptions
{
    ClientId = "your-client-id",
    ClientSecret = "your-client-secret",
});

var calendar = factory.CreateCalendarClient();

var events = await calendar.Calendar.Events.GetAsync(request =>
{
    request.QueryParameters.PerPage = 10;
});

foreach (var e in events?.Data ?? [])
{
    Console.WriteLine($"{e.StartsAt:d} {e.Name}");
}
```

Every module works the same way: `factory.CreateAddressbookClient()`, `factory.CreateRotasClient()`, and so on. The client's properties follow the API's URL structure, so `/calendar/events` becomes `calendar.Calendar.Events`.

Create one factory and reuse it, rather than making a new one for each request.

## Using dependency injection

Put your credentials in configuration, for example `appsettings.json` (or, better, [user secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) or environment variables):

```json
{
  "ChurchSuite": {
    "ClientId": "your-client-id",
    "ClientSecret": "your-client-secret"
  }
}
```

Register ChurchSuite and the module clients you need:

```csharp
using ChurchSuiteDotNet.Calendar;
using ChurchSuiteDotNet.Extensions.DependencyInjection;

builder.Services.AddChurchSuite(builder.Configuration.GetSection("ChurchSuite"));
builder.Services.AddChurchSuiteClient(adapter => new CalendarClient(adapter));
```

Then inject the client wherever you need it:

```csharp
using ChurchSuiteDotNet.Calendar;
using ChurchSuiteDotNet.Calendar.Models;

public class EventsService(CalendarClient calendar)
{
    public async Task<List<Event>> GetEventsAsync()
    {
        var response = await calendar.Calendar.Events.GetAsync();
        return response?.Data ?? [];
    }
}
```

Requests go through `IHttpClientFactory` with standard resilience (retries, timeouts) applied. `AddChurchSuite` returns the `IHttpClientBuilder`, so you can add your own handlers or tune the resilience settings.

The underlying Kiota `IRequestAdapter` is registered as a keyed service (`ServiceCollectionExtensions.RequestAdapterServiceKey`), so ChurchSuiteDotNet can sit alongside other Kiota-based SDKs, such as Microsoft Graph, in the same container.

## Configuration

| Option | Default | Description |
|---|---|---|
| `ClientId` | | The API user's identifier. Required. |
| `ClientSecret` | | The API user's secret. Required. |
| `Scope` | `full_access` | Space-separated [OAuth scopes](https://developer.churchsuite.com/auth) to request, e.g. `calendar.read smallgroups.read`. |
| `BaseUrl` | `https://api.churchsuite.com/v2` | The API's base address. |
| `TokenUrl` | `https://login.churchsuite.com/oauth2/token` | The OAuth token endpoint. CharitySuite accounts use `https://login.charitysuite.com/oauth2/token`. |

Options are validated up front: `ChurchSuiteClientFactory` throws an `ArgumentException` listing every invalid setting, and with dependency injection the host fails to start with an `OptionsValidationException` (or, without a host, the first client you resolve throws it).

## Versioning

All packages are released together and share one version number, so keep them on the same version.

ChurchSuiteDotNet follows [Semantic Versioning](https://semver.org/) for the code written by hand: `ChurchSuiteClientFactory`, `ChurchSuiteOptions`, `ChurchSuiteTokenProvider`, the dependency injection extensions and the `Create<Module>Client()` helpers. Breaking changes to these only come in a major release.

The module clients (`CalendarClient` and the request builders and models under it) are generated from ChurchSuite's OpenAPI specs and follow them. ChurchSuite sometimes restructures a spec without changing the API itself, which can rename or retype generated classes. Those changes ship in **minor** releases, and the release notes list the affected packages under **Breaking changes in generated code**. Code that only calls the clients and uses `var` is rarely affected; you're most likely to notice when you name a generated model type.

Major releases are for breaking changes to the hand-written code, a new major version of the [Kiota](https://learn.microsoft.com/openapi/kiota/) libraries, or a deliberate change to how the clients are generated. Until 1.0, minor releases may also break the hand-written code.

Check the [release notes](https://github.com/JasonElkin/churchsuite-dotnet/releases) before upgrading.

## Contributing

Bug reports and pull requests are welcome. See [CONTRIBUTING.md](https://github.com/JasonElkin/churchsuite-dotnet/blob/main/CONTRIBUTING.md) for how the project is built and released.

## License

[MIT](https://github.com/JasonElkin/churchsuite-dotnet/blob/main/LICENSE)
