namespace MoviePicks.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using MoviePicks.Api.Infrastructure.ThirdPartyApiClients;
using MoviePicks.Contracts;
using MoviePicks.Contracts.DTOs;
using MoviePicks.Contracts.Enums;
using System.ComponentModel.DataAnnotations;

[Route(ApiRoutes.Omdb.Movies.ControllerRoute)]
[ApiController]
public class OmdbMoviesController : ControllerBase
{
    private readonly IOmdbMoviesReader omdbApiMoviesReader;

    public OmdbMoviesController(IOmdbMoviesReader omdbApiMoviesReader)
    {
        this.omdbApiMoviesReader = omdbApiMoviesReader;
    }

    // TODO: Put the title pattern into a query parameter instead of the route to follow RESTful conventions.
    [HttpGet(Name = nameof(SearchOmdbMoviesByTitlePattern))]
    public async Task<ActionResult<IEnumerable<OmdbMovieShortDetailsDto>>> SearchOmdbMoviesByTitlePattern([FromQuery, Required, MinLength(1)] string titlePattern)
    {
        return await this.omdbApiMoviesReader.SearchMoviesByTitle(titlePattern);
    }

    [HttpGet("{imdbId}", Name = nameof(GetMovieByImdbId))]
    public async Task<ActionResult<OmdbMovieDetailsDto>> GetMovieByImdbId(string imdbId)
    {
        var result = await this.omdbApiMoviesReader.GetMovieByImdbId(imdbId, PlotSize.Short);

        if (result != null)
        {
            return result;
        }
        else
        {
            return this.NotFound();
        }
    }
}
