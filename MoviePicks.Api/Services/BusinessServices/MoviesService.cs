namespace MoviePicks.Api.Services.BusinessServices;

using Microsoft.EntityFrameworkCore;
using MoviePicks.Api.Infrastructure.Mappers;
using MoviePicks.Api.Infrastructure.ThirdPartyApiClients;
using MoviePicks.Api.Models;
using MoviePicks.Api.Repositories.Core;
using MoviePicks.Contracts.DTOs;
using MoviePicks.Contracts.Enums;
using System.Collections.Generic;

public class MoviesService : IMoviesService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly IOmdbMoviesReader omdbMovieReader;

    public MoviesService(IUnitOfWork unitOfWork, IOmdbMoviesReader omdbMovieReader)
    {
        this.unitOfWork = unitOfWork;
        this.omdbMovieReader = omdbMovieReader;
    }

    public async Task<List<GenreDto>> GetAllGenresAsync()
    {
        IEnumerable<Genre> genres = await this.unitOfWork.GenreRepository.GetAllAsync();
        return genres.Select(g => new GenreDto { Name = g.Name }).ToList();
    }

    public async Task CreateMovieAsync(MovieViewModel movieViewModel)
    {
        // Add movie to database
        Movie movie = ToMovie(movieViewModel);
        this.unitOfWork.MovieRepository.Add(movie);
        await this.unitOfWork.SaveAsync(); // Must await so the movie.Id is available to save to the MovieGenre intersection table.

        List<Genre> genres = await this.GetOrCreateGenresAsync(movieViewModel.Genre);

        // Add a new row to the MovieGenre intersection table for each genre associated with the movie.
        foreach (var genre in genres)
        {
            // Setting IDs is sufficient for establishing the relationship.
            var movieGenre = new MovieGenre(movieId: movie.Id, genreId: genre.Id);
            movie.MovieGenres.Add(movieGenre);
        }

        await this.unitOfWork.SaveAsync(); // Save the new MovieGenre rows.
    }

    private async Task<List<Genre>> GetOrCreateGenresAsync(string commaSeparatedGenres)
    {
        // TODO: The genres should be converted to a strong data type by the code that retrieves the data from the API.
        //       This will ensure that the data is always in a consistent format and encapsulate such data cleansing logic
        //       to the location where the data is retrieved.
        // TODO: Consider doing this genre parsing closer to the source of the data where it is retrieved from the API.
        //
        // Split the Genre property into individual genres.
        // This might be more robust than needed, because it will handle any number of spaces after the comma.
        // But this is OK because the data comes from the OMDB API which is not under our control.
        var genreNames = commaSeparatedGenres
            .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(g => g.Trim())
            .Distinct();

        // This method checks for the existence of each genre in the database and adds any missing genres.
        // It is crucial to await the completion of this method before attempting to retrieve genres from the database.
        // This ensures that any newly added genres are persisted and available for retrieval.
        await this.EnsureGenresExistInDatabaseAsync(genreNames);

        IEnumerable<Genre> genres = await this.GetGenresFromDatabaseAsync(genreNames);
        return genres.ToList();
    }

    /// <summary>
    /// Ensures that the specified genres exist in the database. If any genres do not exist, they are added.
    /// </summary>
    /// <param name="genreNames">A collection of genre names to ensure exist in the database.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task EnsureGenresExistInDatabaseAsync(IEnumerable<string> genreNames)
    {
        bool genreSaveRequired = false;

        foreach (var genreName in genreNames)
        {
            var genreExistsInDatabase = await this.unitOfWork.GenreRepository.AnyAsync(g => g.Name == genreName);

            if (!genreExistsInDatabase)
            {
                // TODO: Consider implementing a concurrency handling mechanism here to prevent
                // the addition of duplicate genres when multiple requests try to add the same
                // new genre concurrently. This might involve using transactions or implementing
                // a locking mechanism to ensure that only one request can add a new genre at a time.

                // Add a genre to database
                var genre = new Genre { Name = genreName };
                this.unitOfWork.GenreRepository.Add(genre);
                genreSaveRequired = true;
            }
        }

        if (genreSaveRequired)
        {
            // Save changes to assign an Id to any new genres.
            // Special Note:
            //      Await to ensure that all new genres are saved to database because the
            //      next operation depends on the genres being present in the database.
            await this.unitOfWork.SaveAsync();
        }
    }

    // Retrieves the specified genres from the database.
    private async Task<IEnumerable<Genre>> GetGenresFromDatabaseAsync(IEnumerable<string> genreNames)
    {
        var genres = await this.unitOfWork.GenreRepository.FindAsync(genre => genreNames.Contains(genre.Name));

        return genres;
    }

    private static Movie ToMovie(MovieViewModel mvm)
    {
        return new Movie
        {
            Id = mvm.MovieId,
            ImdbId = mvm.ImdbId,
            Rating = mvm.Rating,
            ReviewHeading = mvm.ReviewHeading,
            ReviewComments = mvm.ReviewComments,
            MovieGenres = new List<MovieGenre>(),
        };
    }

    public async Task<bool> DeleteMovieAsync(int movieId)
    {
        bool deleted = this.unitOfWork.MovieRepository.Delete(movieId);
        if (deleted)
        {
            await this.unitOfWork.SaveAsync();
            return true;
        }

        return false;
    }

    public async Task<List<MovieViewModel>> GetFilteredMovieViewModels(MovieFilterDto filterDTO)
    {
        var query = this.ApplyRatingFilter(this.unitOfWork.MovieRepository.GetAsQueryable(), filterDTO.Rating);

        query = this.ApplyGenreFilter(query, filterDTO.Genres, filterDTO.GenreFilterMode);

        var movies = await query.Include(m => m.MovieGenres).ThenInclude(mg => mg.Genre).ToListAsync();

        return await this.ConvertToMovieViewModels(movies);
    }

    public async Task<List<MovieViewModel>> GetAllMovieViewModels()
    {
        List<MovieViewModel> result = new List<MovieViewModel>();

        List<CompositeMovie> list = await this.GetAllCompositeMovies();

        foreach (CompositeMovie cmovie in list)
        {
            result.Add(cmovie.ToMovieViewModel());
        }

        return result;
    }

    public async Task<MovieViewModel> GetMovieViewModel(int movieId, PlotSize plotSize)
    {
        Movie movie = await Task.Run(() => this.unitOfWork.MovieRepository.GetMovie(movieId));
        OmdbMovieDetailsDto omdbMovieDetails = await this.GetCachedOmdbMovieDetails(movie.ImdbId, plotSize);
        return new CompositeMovie(movie, omdbMovieDetails).ToMovieViewModel();
    }

    public async Task UpdateMovie(Movie movie)
    {
        await Task.Run(() => this.unitOfWork.MovieRepository.UpdateMovie(movie));
        await this.unitOfWork.SaveAsync();
    }

    private async Task<List<CompositeMovie>> GetAllCompositeMovies()
    {
        IEnumerable<Movie> movies = await this.unitOfWork.MovieRepository.GetAllAsync();
        var compositeMovies = new List<CompositeMovie>();

        foreach (Movie movie in movies)
        {
            OmdbMovieDetailsDto omdbMovieDetails = await this.GetCachedOmdbMovieDetails(movie.ImdbId, PlotSize.Short);

            compositeMovies.Add(new CompositeMovie(movie, omdbMovieDetails));
        }

        return compositeMovies;
    }

    /// <summary>
    /// Retrieves OMDB movie details. Caching is handled transparently by the
    /// cache implementation of IOmdbMoviesReader.
    /// </summary>
    private async Task<OmdbMovieDetailsDto> GetCachedOmdbMovieDetails(string imdbId, PlotSize plotSize)
    {
        return await this.omdbMovieReader.GetMovieByImdbId(imdbId, plotSize);
    }

    private IQueryable<Movie> ApplyRatingFilter(IQueryable<Movie> query, int? rating)
    {
        if (rating.HasValue)
        {
            return query.Where(movie => movie.Rating >= rating.Value);
        }

        return query;
    }

    private IQueryable<Movie> ApplyGenreFilter(IQueryable<Movie> query, List<string> genres, GenreFilterMode genreFilterMode)
    {
        if (!genres.Any())
        {
            return query;
        }

        if (genreFilterMode == GenreFilterMode.MatchAll)
        {
            // Filter out movies that do not have all matching genres.
            foreach (var genreName in genres)
            {
                // This subquery finds movies that have the current genre.
                var subQuery = this
                    .unitOfWork
                    .MovieRepository
                    .FindAll(movie =>
                        movie
                        .MovieGenres.Any(mg => mg.Genre.Name == genreName));

                // Intersect with the main query to progressively narrow down the movies.
                query = query.Intersect(subQuery);
            }
        }
        else // GenreFilterMode.MatchAny
        {
            query = query.Where(movie => movie.MovieGenres.Any(mg => genres.Contains(mg.Genre.Name)));
        }

        return query;
    }

    private async Task<List<MovieViewModel>> ConvertToMovieViewModels(List<Movie> movies)
    {
        var movieViewModels = new List<MovieViewModel>();

        foreach (var movie in movies)
        {
            OmdbMovieDetailsDto omdbMovieDetails = await this.GetCachedOmdbMovieDetails(movie.ImdbId, PlotSize.Short);
            movieViewModels.Add(new CompositeMovie(movie, omdbMovieDetails).ToMovieViewModel());
        }

        return movieViewModels;
    }
}
