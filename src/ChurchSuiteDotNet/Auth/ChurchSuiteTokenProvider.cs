using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Kiota.Abstractions.Authentication;

namespace ChurchSuiteDotNet.Auth;

/// <summary>
/// Obtains and caches ChurchSuite access tokens using the OAuth2 client credentials grant.
/// Thread-safe; share a single instance across clients.
/// </summary>
public sealed class ChurchSuiteTokenProvider : IAccessTokenProvider, IDisposable
{
    private const double ExpiryBuffer = 0.1;

    private readonly ChurchSuiteOptions _options;
    private readonly Func<HttpClient> _httpClientAccessor;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private string? _token;
    private DateTimeOffset _expiresAt;

    /// <param name="options">Credentials and endpoints.</param>
    /// <param name="httpClient">Client used to call the token endpoint.</param>
    /// <param name="timeProvider">Optional clock, mainly for testing.</param>
    public ChurchSuiteTokenProvider(ChurchSuiteOptions options, HttpClient httpClient, TimeProvider? timeProvider = null)
        : this(options, () => httpClient, timeProvider)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
    }

    /// <param name="options">Credentials and endpoints.</param>
    /// <param name="httpClientAccessor">Called for each token request; lets callers use <c>IHttpClientFactory</c>.</param>
    /// <param name="timeProvider">Optional clock, mainly for testing.</param>
    public ChurchSuiteTokenProvider(ChurchSuiteOptions options, Func<HttpClient> httpClientAccessor, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(httpClientAccessor);
        _options = options;
        _httpClientAccessor = httpClientAccessor;
        _timeProvider = timeProvider ?? TimeProvider.System;
        AllowedHostsValidator = new AllowedHostsValidator([options.BaseUrl.Host]);
    }

    /// <inheritdoc />
    public AllowedHostsValidator AllowedHostsValidator { get; }

    /// <inheritdoc />
    public async Task<string> GetAuthorizationTokenAsync(
        Uri uri,
        Dictionary<string, object>? additionalAuthenticationContext = null,
        CancellationToken cancellationToken = default)
    {
        if (TryGetCachedToken(out var cached))
        {
            return cached;
        }

        await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (TryGetCachedToken(out cached))
            {
                return cached;
            }

            return await RefreshAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose() => _refreshLock.Dispose();

    private bool TryGetCachedToken(out string token)
    {
        var current = Volatile.Read(ref _token);
        if (current is not null && _timeProvider.GetUtcNow() < _expiresAt)
        {
            token = current;
            return true;
        }

        token = string.Empty;
        return false;
    }

    private async Task<string> RefreshAsync(CancellationToken cancellationToken)
    {
        // ChurchSuite expects the client id and secret as HTTP Basic credentials, not in the form body.
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.ClientId}:{_options.ClientSecret}"));
        using var request = new HttpRequestMessage(HttpMethod.Post, _options.TokenUrl)
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Basic", credentials) },
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["scope"] = _options.Scope,
            }),
        };

        var response = await _httpClientAccessor().SendAsync(request, cancellationToken).ConfigureAwait(false);
        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                throw new HttpRequestException(
                    $"Failed to obtain ChurchSuite access token: {(int)response.StatusCode} {response.ReasonPhrase}. {body}",
                    inner: null,
                    response.StatusCode);
            }

            var token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrEmpty(token?.AccessToken))
            {
                throw new InvalidOperationException("ChurchSuite token response did not contain an access token.");
            }

            var lifetime = TimeSpan.FromSeconds(token.ExpiresIn * (1 - ExpiryBuffer));
            _expiresAt = _timeProvider.GetUtcNow() + lifetime;
            Volatile.Write(ref _token, token.AccessToken);
            return token.AccessToken;
        }
    }
}
