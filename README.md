# ChurchSuiteDotNet

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

You'll need API credentials (a client ID and secret) from your ChurchSuite account. See [ChurchSuite's API documentation](https://github.com/ChurchSuite/churchsuite-api) for how to create them.

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

## Configuration

| Option | Default | Description |
|---|---|---|
| `ClientId` | | Your OAuth client ID. Required. |
| `ClientSecret` | | Your OAuth client secret. Required. |
| `Scope` | `full_access` | The OAuth scope to request. |
| `BaseUrl` | `https://api.churchsuite.com/v2` | The API's base address. |
| `TokenUrl` | `https://login.churchsuite.com/oauth2/token` | The OAuth token endpoint. |

## Versioning

All packages are released together and share one version number, so keep them on the same version. The project is pre-1.0, so a minor version bump may include breaking changes; check the [release notes](https://github.com/JasonElkin/churchsuite-dotnet/releases) before upgrading.

## Contributing

Bug reports and pull requests are welcome. See [CONTRIBUTING.md](https://github.com/JasonElkin/churchsuite-dotnet/blob/main/CONTRIBUTING.md) for how the project is built and released.

## License

[MIT](https://github.com/JasonElkin/churchsuite-dotnet/blob/main/LICENSE)
