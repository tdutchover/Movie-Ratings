namespace MoviePicks.AppHost;

public class RedisSettings
{
    public bool UseCloudRedis { get; set; } = false;
    public bool UsePersistentCache { get; set; } = true; // Scenario B
    public string VolumeName { get; set; } = "movie-picks-redis-data";
}
