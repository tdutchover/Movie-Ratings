using Microsoft.Extensions.Configuration;
using MoviePicks.AppHost;
using MoviePicks.Contracts;

var builder = DistributedApplication.CreateBuilder(args);

var redisSettings = builder.Configuration.GetSection("Redis").Get<RedisSettings>() ?? new();

IResourceBuilder<IResourceWithConnectionString> redis;

if (redisSettings.UseCloudRedis)
{
    // Use an external Redis server configured in AppHost user secrets
    // under ConnectionStrings:redis-cache as specified by BackendInfrastructureConstants.RedisResourceConnectionName.
    // This cloud connection string path has not been tested.
    redis = builder.AddConnectionString(BackendInfrastructureConstants.RedisResourceConnectionName);
}
else
{
    // Use local Docker Desktop containerized Redis
    var localRedis = builder.AddRedis(BackendInfrastructureConstants.RedisResourceConnectionName)
                            .WithRedisInsight(); // Redis Insights is a GUI for analyzing Redis data.

    if (redisSettings.UsePersistentCache)
    {
        // Configure Redis to snapshot after at least 60 seconds have elapsed
        // since the last save and at least one key-changing operation has occurred.
        // Store snapshots in a named Docker volume so saved cache data survives
        // container recreation. Unsaved changes can be lost after an abrupt shutdown.
        localRedis.WithPersistence(interval: TimeSpan.FromMinutes(1), keysChangedThreshold: 1)
                  .WithDataVolume(redisSettings.VolumeName);
    }
    redis = localRedis;
}

var backendApi = builder.AddProject<Projects.MoviePicks_Api>(
    AspireResourceName
        .ServiceDiscovery
        .Project
        .MoviePicksApi)
        .WithReference(redis);

// Frontend web UI project
builder.AddProject<Projects.MoviePicks_Web>(
    AspireResourceName
        .AppHostOnly
        .Project
        .MoviePicksWeb)
    .WithReference(backendApi);

builder.Build().Run();