using ChurchSuiteDotNet.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;

namespace ChurchSuiteDotNet.Extensions.DependencyInjection;

/// <summary>Service registration for ChurchSuite clients. Works with any <see cref="IServiceCollection"/>.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Name of the <see cref="HttpClient"/> used for API calls.</summary>
    public const string ApiHttpClientName = "ChurchSuite";

    /// <summary>Name of the <see cref="HttpClient"/> used to request tokens.</summary>
    public const string AuthHttpClientName = "ChurchSuite.Auth";

    /// <summary>The default configuration section, <c>ChurchSuite</c>.</summary>
    public const string DefaultConfigurationSection = "ChurchSuite";

    /// <summary>
    /// Key of the ChurchSuite <see cref="IRequestAdapter"/>. It is registered as a keyed service so it doesn't
    /// clash with other Kiota-based SDKs in the same container.
    /// </summary>
    public const string RequestAdapterServiceKey = "ChurchSuite";

    /// <summary>Registers ChurchSuite services, configured by code.</summary>
    /// <returns>The builder for the API <see cref="HttpClient"/>, for further customisation.</returns>
    public static IHttpClientBuilder AddChurchSuite(this IServiceCollection services, Action<ChurchSuiteOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        services.AddOptions<ChurchSuiteOptions>().Configure(configure);
        return services.AddChurchSuiteCore();
    }

    /// <summary>Registers ChurchSuite services, binding options from a configuration section.</summary>
    /// <returns>The builder for the API <see cref="HttpClient"/>, for further customisation.</returns>
    public static IHttpClientBuilder AddChurchSuite(this IServiceCollection services, IConfiguration configurationSection)
    {
        ArgumentNullException.ThrowIfNull(configurationSection);
        services.AddOptions<ChurchSuiteOptions>().Bind(configurationSection);
        return services.AddChurchSuiteCore();
    }

    /// <summary>
    /// Registers a module client (e.g. <c>CalendarClient</c>) as a transient service.
    /// Call <c>AddChurchSuite</c> first.
    /// </summary>
    public static IServiceCollection AddChurchSuiteClient<T>(this IServiceCollection services, Func<IRequestAdapter, T> clientFactory)
        where T : BaseRequestBuilder
    {
        ArgumentNullException.ThrowIfNull(clientFactory);
        return services.AddTransient(sp => clientFactory(sp.GetRequiredKeyedService<IRequestAdapter>(RequestAdapterServiceKey)));
    }

    private static IHttpClientBuilder AddChurchSuiteCore(this IServiceCollection services)
    {
        // Validated when the host starts, or on first use outside a host.
        services.AddOptions<ChurchSuiteOptions>().ValidateDataAnnotations().ValidateOnStart();

        services.AddHttpClient(AuthHttpClientName).AddStandardResilienceHandler();

        services.AddSingleton(sp => new ChurchSuiteTokenProvider(
            sp.GetRequiredService<IOptions<ChurchSuiteOptions>>().Value,
            () => sp.GetRequiredService<IHttpClientFactory>().CreateClient(AuthHttpClientName)));

        services.AddKeyedTransient<IRequestAdapter>(RequestAdapterServiceKey, (sp, _) =>
        {
            var options = sp.GetRequiredService<IOptions<ChurchSuiteOptions>>().Value;
            var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient(ApiHttpClientName);
            var auth = new BaseBearerTokenAuthenticationProvider(sp.GetRequiredService<ChurchSuiteTokenProvider>());
            return new HttpClientRequestAdapter(auth, httpClient: http)
            {
                BaseUrl = options.BaseUrl.ToString().TrimEnd('/'),
            };
        });

        // Retries are handled by the resilience pipeline, so Kiota's RetryHandler is skipped.
        var kiotaHandlers = KiotaClientFactory.GetDefaultHandlerActivatableTypes()
            .Where(t => t != typeof(Microsoft.Kiota.Http.HttpClientLibrary.Middleware.RetryHandler))
            .ToArray();

        var builder = services.AddHttpClient(ApiHttpClientName);
        foreach (var handler in kiotaHandlers)
        {
            services.AddTransient(handler);
            builder.AddHttpMessageHandler(sp => (DelegatingHandler)sp.GetRequiredService(handler));
        }

        builder.AddStandardResilienceHandler();
        return builder;
    }
}
