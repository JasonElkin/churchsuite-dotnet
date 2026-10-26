using System.Net;
using System.Text;
using ChurchSuiteDotNet.Auth;
using ChurchSuiteDotNet.Calendar;
using ChurchSuiteDotNet.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace ChurchSuiteDotNet.Tests;

[TestFixture]
public class TokenProviderTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Calls;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Interlocked.Increment(ref Calls);
            await Task.Yield();
            return respond(request);
        }
    }

    private static HttpResponseMessage Token(int expiresIn = 3600) => new(HttpStatusCode.OK)
    {
        Content = new StringContent($$"""{"token_type":"Bearer","expires_in":{{expiresIn}},"access_token":"abc"}""", Encoding.UTF8, "application/json"),
    };

    private static readonly ChurchSuiteOptions Options = new() { ClientId = "id", ClientSecret = "secret" };
    private static readonly Uri Api = new("https://api.churchsuite.com/v2/x");

    private sealed class FakeTime : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Test]
    public async Task CachesTokenUntilNearExpiry()
    {
        var handler = new StubHandler(_ => Token(100));
        var time = new FakeTime();
        using var provider = new ChurchSuiteTokenProvider(Options, new HttpClient(handler), time);

        Assert.That(await provider.GetAuthorizationTokenAsync(Api), Is.EqualTo("abc"));
        await provider.GetAuthorizationTokenAsync(Api);
        Assert.That(handler.Calls, Is.EqualTo(1));

        time.Now += TimeSpan.FromSeconds(95);
        await provider.GetAuthorizationTokenAsync(Api);
        Assert.That(handler.Calls, Is.EqualTo(2));
    }

    [Test]
    public async Task SendsCredentialsWithBasicAuth()
    {
        HttpRequestMessage? sent = null;
        string? body = null;
        var handler = new StubHandler(request =>
        {
            sent = request;
            body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return Token();
        });
        using var provider = new ChurchSuiteTokenProvider(Options, new HttpClient(handler));

        await provider.GetAuthorizationTokenAsync(Api);

        Assert.That(sent!.RequestUri, Is.EqualTo(Options.TokenUrl));
        Assert.That(sent.Headers.Authorization?.Scheme, Is.EqualTo("Basic"));
        Assert.That(sent.Headers.Authorization?.Parameter, Is.EqualTo(Convert.ToBase64String(Encoding.UTF8.GetBytes("id:secret"))));
        Assert.That(body, Is.EqualTo("grant_type=client_credentials&scope=full_access"));
    }

    [Test]
    public async Task ConcurrentRequestsRefreshOnce()
    {
        var handler = new StubHandler(_ => Token());
        using var provider = new ChurchSuiteTokenProvider(Options, new HttpClient(handler));

        await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => provider.GetAuthorizationTokenAsync(Api)));

        Assert.That(handler.Calls, Is.EqualTo(1));
    }

    [Test]
    public void FailureIncludesStatusAndBody()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("bad creds") });
        using var provider = new ChurchSuiteTokenProvider(Options, new HttpClient(handler));

        var ex = Assert.ThrowsAsync<HttpRequestException>(() => provider.GetAuthorizationTokenAsync(Api));
        Assert.That(ex!.Message, Does.Contain("bad creds"));
    }

    [Test]
    public void DiRegistersModuleClient()
    {
        var services = new ServiceCollection();
        services.AddChurchSuite(o => { o.ClientId = "id"; o.ClientSecret = "secret"; });
        services.AddChurchSuiteClient(a => new CalendarClient(a));

        using var sp = services.BuildServiceProvider();

        Assert.That(sp.GetRequiredService<CalendarClient>(), Is.Not.Null);
    }

    [Test]
    public void FactoryCreatesModuleClient()
    {
        using var factory = new ChurchSuiteClientFactory(Options);

        Assert.That(factory.CreateCalendarClient(), Is.Not.Null);
        Assert.That(factory.CreateRequestAdapter().BaseUrl, Is.EqualTo("https://api.churchsuite.com/v2"));
    }
}
