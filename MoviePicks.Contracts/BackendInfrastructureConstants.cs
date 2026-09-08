namespace MoviePicks.Contracts;

/// <summary>
/// Shared constants only for backend infrastructure.
/// </summary>
/// <remarks>
/// The Web frontend project should not use these constants; they are backend infrastructure only.
/// </remarks>
public static class BackendInfrastructureConstants
{
    /// <summary>
    /// Infrastructure constant shared between the Aspire MoviePicks.AppHost project and
    /// the MoviePicks.Api service project.
    /// </summary>
    /// <remarks>
    /// This constant lives in MoviePicks.Contracts rather than in MoviePicks.Api because
    /// MoviePicks.AppHost needs them for Aspire resource wiring, and the MoviePicks.AppHost-to-MoviePicks.Api
    /// project reference is an Aspire service-discovery reference — not a normal code-sharing reference.
    /// MoviePicks.Contracts is the lowest-level project that both AppHost and Api can reference
    /// without coupling the Aspire AppHost orchestrator project to the MoviePicks.Api project.
    /// </remarks>
    public const string RedisResourceConnectionName = "redis-cache";
}
