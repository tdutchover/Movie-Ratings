namespace MoviePicks.Api.Infrastructure.ThirdPartyApiClients;

using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;
using MoviePicks.Api.Configuration;

/// <summary>
/// Registers OMDb API integration components, including configuration validation
/// and the HTTP client used to communicate with the external service.
/// </summary>
public static class OmdbClientServiceCollectionExtensions
{
    /// <summary>
    /// Adds OMDb configuration and HTTP client to the service container.
    /// Fails fast during application startup if configuration is invalid.
    /// </summary>
    public static IServiceCollection AddOmdbIntegration(this IServiceCollection services, IConfiguration configuration)
    {
        // Validate OMDb configuration at startup so invalid settings fail fast,
        // instead of causing runtime errors when the client is first used.
        // The built-in [Url] attribute is intentionally not used because it allows
        // unsupported schemes (e.g., ftp://); this check restricts values to
        // HTTP/HTTPS URIs.
        services.AddOptions<OmdbOptions>()
            .Bind(configuration.GetSection(OmdbOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(
                options =>
                    Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri)
                    && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps),
                "Invalid configuration: Omdb:BaseUrl must be a valid absolute HTTP/HTTPS URI. Check appsettings.json.")
            .ValidateOnStart();

        // Register OmdbMoviesReader as a typed HTTP client.
        // - Lifetime: Transient (a new OmdbMoviesReader instance per DI request).
        // - The underlying HttpMessageHandler and its TCP connection pool are managed
        //   separately by IHttpClientFactory, so Transient here does not cause socket exhaustion.
        // - Registered by concrete type (not by IOmdbMoviesReader) so that the decorator
        //   CachedOmdbApiMoviesReader below can resolve it directly without creating a circular dependency.
        services.AddHttpClient<OmdbMoviesReader>(
            (serviceProvider, client) =>
            {
                var options = serviceProvider
                    .GetRequiredService<IOptions<OmdbOptions>>()
                    .Value;
                client.BaseAddress = new Uri(options.BaseUrl);
            });

        // Register the caching decorator as the implementation of IOmdbMoviesReader.
        // - Lifetime: Scoped (one instance per HTTP request).
        // - Decorator pattern: CachedOmdbApiMoviesReader wraps OmdbMoviesReader and
        //   adds HybridCache (L1 in-memory + L2 Redis) transparently. All consumers
        //   that inject IOmdbMoviesReader receive the cached version automatically.
        // - HybridCache is registered as a singleton by ConfigureServices and shared
        //   across requests, so these scoped readers use the same L1 in-memory cache.
        services.AddScoped<IOmdbMoviesReader>(sp =>
            new CachedOmdbApiMoviesReader(
                innerReader: sp.GetRequiredService<OmdbMoviesReader>(),
                hybridCache: sp.GetRequiredService<HybridCache>()));

        return services;
    }
}
