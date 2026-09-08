namespace MoviePicks.Api.Infrastructure.ThirdPartyApiClients;

using MoviePicks.Api.Models;
using MoviePicks.Contracts.DTOs;
using MoviePicks.Contracts.Enums;

public class OmdbMoviesReader : IOmdbMoviesReader
{
    private readonly HttpClient httpClient;
    private readonly string apiKey;

    public OmdbMoviesReader(HttpClient httpClient, IConfiguration configuration)
    {
        this.httpClient = httpClient;
        this.apiKey = configuration[ConfigurationManagerKeys.OpenMovieDatabaseApiKey] ??
            throw new ArgumentNullException(nameof(configuration), $"Failed to access configuration for key {ConfigurationManagerKeys.OpenMovieDatabaseApiKey}");
    }

    public async Task<OmdbMovieDetailsDto> GetMovieByImdbId(string imdbId, PlotSize plotSize)
    {
        string url = $"?apikey={this.apiKey}&i={imdbId}";

        if (plotSize == PlotSize.Full)
        {
            url += "&plot=full";    // Retrieve full plot, not the short plot
        }

        OmdbMovieDetailsDto? response = await this.httpClient.GetFromJsonAsync<OmdbMovieDetailsDto>(url);

        if (response == null)
        {
            throw new InvalidOperationException("The JSON response is empty or invalid.");
        }

        // OMDb can report failure in the JSON body even when the HTTP status is 200.
        if (!IsSuccessfulOmdbResponse(response.Response))
        {
            throw new InvalidOperationException("OMDb did not return successful movie details.");
        }

        return response;
    }

    public async Task<List<OmdbMovieShortDetailsDto>> SearchMoviesByTitle(string title)
    {
        var url = $"?apikey={this.apiKey}&s={title}";

        OmdbMovieSearchResult? result = await this.httpClient.GetFromJsonAsync<OmdbMovieSearchResult>(url);

        // Return results only when OMDb reports success and provides a non-null search collection.
        if (result?.search != null && IsSuccessfulOmdbResponse(result.Response))
        {
            return result.search.ToList();
        }
        else
        {
            // Typically will get here if the server doesn't have any movies to return because the title-pattern doesn't match any movies.
            // In this case, result will be valid. But result.Response will be "False".
            //
            // TODO: If result is null, then log something. Or perhaps throw InvalidOperationException and allow the caller to handle it and log it.
            return new List<OmdbMovieShortDetailsDto>();
        }
    }

    private static bool IsSuccessfulOmdbResponse(string? responseStatus)
    {
        return string.Equals(
            responseStatus,
            "True",
            StringComparison.OrdinalIgnoreCase);
    }

    private static class ConfigurationManagerKeys // TODO Move to it's own file when more keys are needed by other files
    {
        public static readonly string OpenMovieDatabaseApiKey = "OpenMovieDatabaseApiKey";
    }
}
