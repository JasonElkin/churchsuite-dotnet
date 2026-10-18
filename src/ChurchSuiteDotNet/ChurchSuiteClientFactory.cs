using ChurchSuiteDotNet.Auth;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;

namespace ChurchSuiteDotNet;

/// <summary>
/// Creates authenticated request adapters for the generated ChurchSuite module clients.
/// Works in any .NET application without dependency injection; module packages add
/// helpers such as <c>CreateCalendarClient()</c>.
/// </summary>
public sealed class ChurchSuiteClientFactory : IDisposable
{
    private readonly ChurchSuiteOptions _options;
    private readonly BaseBearerTokenAuthenticationProvider _authenticationProvider;
    private readonly ChurchSuiteTokenProvider? _ownedTokenProvider;
    private readonly HttpClient? _httpClient;

    /// <summary>Creates a factory using default Kiota HTTP handlers and its own <see cref="HttpClient"/>s.</summary>
    public ChurchSuiteClientFactory(ChurchSuiteOptions options)
        : this(options, httpClient: null)
    {
    }

    /// <summary>
    /// Creates a factory.
    /// </summary>
    /// <param name="options">Credentials and endpoints.</param>
    /// <param name="httpClient">
    /// Optional client used for API calls and token requests. It is not disposed by the factory.
    /// When omitted, a client with the default Kiota handler pipeline is created.
    /// </param>
    public ChurchSuiteClientFactory(ChurchSuiteOptions options, HttpClient? httpClient)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        _httpClient = httpClient ?? KiotaClientFactory.Create();
        _ownedTokenProvider = new ChurchSuiteTokenProvider(options, _httpClient);
        _authenticationProvider = new BaseBearerTokenAuthenticationProvider(_ownedTokenProvider);
        _ownsHttpClient = httpClient is null;
    }

    private readonly bool _ownsHttpClient;

    /// <summary>Creates a factory that authenticates using an existing token provider and HTTP client.</summary>
    public ChurchSuiteClientFactory(ChurchSuiteOptions options, IAccessTokenProvider tokenProvider, HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(tokenProvider);
        ArgumentNullException.ThrowIfNull(httpClient);
        _options = options;
        _httpClient = httpClient;
        _authenticationProvider = new BaseBearerTokenAuthenticationProvider(tokenProvider);
    }

    /// <summary>Creates a new request adapter pointed at <see cref="ChurchSuiteOptions.BaseUrl"/>.</summary>
    public IRequestAdapter CreateRequestAdapter()
    {
        var adapter = new HttpClientRequestAdapter(_authenticationProvider, httpClient: _httpClient)
        {
            BaseUrl = _options.BaseUrl.ToString().TrimEnd('/'),
        };
        return adapter;
    }

    /// <summary>Creates a client for a module, e.g. <c>factory.Create(a =&gt; new CalendarClient(a))</c>.</summary>
    public T Create<T>(Func<IRequestAdapter, T> clientFactory) where T : BaseRequestBuilder
    {
        ArgumentNullException.ThrowIfNull(clientFactory);
        return clientFactory(CreateRequestAdapter());
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _ownedTokenProvider?.Dispose();
        if (_ownsHttpClient)
        {
            _httpClient?.Dispose();
        }
    }
}
