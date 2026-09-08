namespace MoviePicks.Api.Infrastructure.ThirdPartyApiClients;

using Microsoft.Extensions.Caching.Hybrid;
using MoviePicks.Contracts.DTOs;
using MoviePicks.Contracts.Enums;

public class CachedOmdbApiMoviesReader : IOmdbMoviesReader
{
    private readonly IOmdbMoviesReader innerReader;
    private readonly HybridCache hybridCache;

    public CachedOmdbApiMoviesReader(IOmdbMoviesReader innerReader, HybridCache hybridCache)
    {
        this.innerReader = innerReader;
        this.hybridCache = hybridCache;
    }

    /// <summary>
    /// Retrieves OMDB movie details from the cache if available, otherwise fetches from the
    /// OMDB API and caches the result. Cache key is a compound key built from movie imdbId
    /// and plotSize since Full and Short plots have different payloads.
    /// </summary>
    public async Task<OmdbMovieDetailsDto> GetMovieByImdbId(string imdbId, PlotSize plotSize)
    {
        string cacheKey = $"omdb:{imdbId}:{plotSize}";

        return await this.hybridCache.GetOrCreateAsync(
            cacheKey,
            async cancel => await this.innerReader.GetMovieByImdbId(imdbId, plotSize),
            cancellationToken: CancellationToken.None);
    }

    public Task<List<OmdbMovieShortDetailsDto>> SearchMoviesByTitle(string title)
    {
        // Title searches are intentionally not cached because this is currently a
        // single-user application, and I anticipate that the user will not search
        // for the same title frequently enough to benefit much from caching.
        // This is an assumption, not something verified through usage measurements.
        // Reconsider caching if the user repeats searches frequently or the application
        // gains many users who search for the same popular movies.
        return this.innerReader.SearchMoviesByTitle(title);
    }
}
