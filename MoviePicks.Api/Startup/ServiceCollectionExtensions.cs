namespace MoviePicks.Api.Startup;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using MoviePicks.Api.Infrastructure.ThirdPartyApiClients;
using MoviePicks.Api.Models;
using MoviePicks.Api.Repositories;
using MoviePicks.Api.Repositories.Core;
using MoviePicks.Api.Services.BusinessServices;

public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection ConfigureServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        // Add the IProblemDetailsService implementation that is used by
        // both ExceptionHandlerMiddleware and UseStatusCodePagesMiddleware
        // to provide ProblemDetails responses.
        services.AddProblemDetails();

        services.AddControllers();

        services.AddEndpointsApiExplorer();
        services.AddOpenApi();

        var cacheSettings = configuration.GetSection("HybridCache").Get<Configuration.HybridCacheOptions>() ?? new ();

#pragma warning disable EXTEXP0018 // Still required as of .NET 10 for the Extensions library

        // AddHybridCache registers HybridCache as a singleton.
        // Requests handled by this API instance share the same cache service
        // and L1 in-memory cache.
        services.AddHybridCache(options =>
        {
            options.DefaultEntryOptions = new HybridCacheEntryOptions
            {
                LocalCacheExpiration = TimeSpan.FromHours(cacheSettings.LocalCacheExpirationHours), // L1 (in-memory) Expiry
                Expiration = TimeSpan.FromDays(cacheSettings.DistributedCacheExpirationDays),       // L2 (redis) Expiry
            };
            options.MaximumPayloadBytes = cacheSettings.MaxPayloadBytes;
        });
#pragma warning restore EXTEXP0018

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IMoviesService, MoviesService>();
        services.AddScoped<IMovieRepository, DbMovieRepository>();    // database repository service

        services.AddOmdbIntegration(configuration);

        // Secrets are configured as follows:
        //      Development environment: secrets are retrieved from the local secrets.json file on a developer's machine.
        //      Production environment:  secrets are retrieved from environment variables.
        // The following secrets are used by this backend service:
        //      OMDB API Key
        //      database connection string
        services.AddDbContext<DbMovieContext>(options => options.UseSqlServer(configuration.GetConnectionString("movieDatabaseSqlServer")));

        // Alternate SQLite DB for possible use during deployment in case SQLServer hosting costs money
        //builder.Services.AddDbContext<DbMovieContext>(options => options.UseSqlite("Data Source=MovieDb.db"));

        services.AddHttpClient();   // Needed to call web api. Registers service IHttpClientFactory

        return services;
    }
}
