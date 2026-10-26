using ChurchSuiteDotNet.Calendar;
using ChurchSuiteDotNet.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;
using NUnit.Framework;

namespace ChurchSuiteDotNet.Tests;

[TestFixture]
public class ServiceCollectionExtensionsTests
{
    [Test]
    public void DoesNotClashWithOtherKiotaClients()
    {
        var other = new HttpClientRequestAdapter(new AnonymousAuthenticationProvider()) { BaseUrl = "https://other.example" };
        var services = new ServiceCollection();
        services.AddSingleton<IRequestAdapter>(other);
        services.AddChurchSuite(o => { o.ClientId = "id"; o.ClientSecret = "secret"; });
        services.AddChurchSuiteClient(a => new CalendarClient(a));

        using var sp = services.BuildServiceProvider();
        var calendar = sp.GetRequiredService<CalendarClient>();

        Assert.That(sp.GetRequiredService<IRequestAdapter>(), Is.SameAs(other));
        Assert.That(calendar.Calendar.Events.ToGetRequestInformation().URI.Host, Is.EqualTo("api.churchsuite.com"));
    }

    [Test]
    public void InvalidOptionsFailOnFirstUse()
    {
        var services = new ServiceCollection();
        services.AddChurchSuite(o => o.ClientId = "id");
        services.AddChurchSuiteClient(a => new CalendarClient(a));

        using var sp = services.BuildServiceProvider();

        var ex = Assert.Throws<OptionsValidationException>(() => sp.GetRequiredService<CalendarClient>());
        Assert.That(ex!.Message, Does.Contain("ClientSecret"));
    }
}
