namespace MoviePicks.Api.Infrastructure.ThirdPartyApiClients;

using MoviePicks.Contracts.DTOs;
using MoviePicks.Contracts.Enums;

/// <summary>
/// Service to read public information about movies
/// </summary>
public interface IOmdbMoviesReader
{
    Task<List<OmdbMovieShortDetailsDto>> SearchMoviesByTitle(string title);

    /// <summary>
    /// Retrieves OMDb movie details for the specified IMDb ID and plot size.
    /// </summary>
    Task<OmdbMovieDetailsDto> GetMovieByImdbId(string imdbId, PlotSize plotSize);
}
