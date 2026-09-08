namespace MoviePicks.Api.Configuration;

public class HybridCacheOptions
{
    private const long OneMegabyteLimitPerCachedItem = 1024 * 1024;

    public int LocalCacheExpirationHours { get; set; } = 1;

    public int DistributedCacheExpirationDays { get; set; } = 30;

    public long MaxPayloadBytes { get; set; } = OneMegabyteLimitPerCachedItem;
}
