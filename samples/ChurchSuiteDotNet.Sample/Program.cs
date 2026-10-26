using ChurchSuiteDotNet;
using ChurchSuiteDotNet.Calendar;
using ChurchSuiteDotNet.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

// Lists a few calendar events twice: once without dependency injection, once with it.
// Credentials come from user secrets or ChurchSuite__ClientId / ChurchSuite__ClientSecret environment variables.
var configuration = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .AddEnvironmentVariables()
    .Build();
var section = configuration.GetSection("ChurchSuite");
var options = section.Get<ChurchSuiteOptions>() ?? new ChurchSuiteOptions();

if (string.IsNullOrEmpty(options.ClientId) || string.IsNullOrEmpty(options.ClientSecret))
{
    Console.Error.WriteLine("""
        Set your credentials first, from samples/ChurchSuiteDotNet.Sample:

          dotnet user-secrets set ChurchSuite:ClientId <your identifier>
          dotnet user-secrets set ChurchSuite:ClientSecret <your secret>
        """);
    return 1;
}

var ok = true;

Console.WriteLine("Without dependency injection:");
using (var factory = new ChurchSuiteClientFactory(options))
{
    ok &= await PrintEventsAsync(factory.CreateCalendarClient());
}

Console.WriteLine();
Console.WriteLine("With dependency injection:");
var services = new ServiceCollection();
services.AddChurchSuite(section);
services.AddChurchSuiteClient(adapter => new CalendarClient(adapter));
await using (var provider = services.BuildServiceProvider())
{
    ok &= await PrintEventsAsync(provider.GetRequiredService<CalendarClient>());
}

return ok ? 0 : 1;

static async Task<bool> PrintEventsAsync(CalendarClient calendar)
{
    try
    {
        var events = await calendar.Calendar.Events.GetAsync(request => request.QueryParameters.PerPage = 5);
        var data = events?.Data ?? [];
        Console.WriteLine($"  {data.Count} event(s)");
        foreach (var e in data)
        {
            Console.WriteLine($"  {e.StartsAt:g}  {e.Name}");
        }
        return true;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  Failed: {ex.GetType().Name}: {ex.Message}");
        return false;
    }
}
