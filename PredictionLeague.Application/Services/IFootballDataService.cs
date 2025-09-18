using PredictionLeague.Application.FootballApi.DTOs;

namespace PredictionLeague.Application.Services;

public interface IFootballDataService
{
    Task<IEnumerable<TeamResponse>> GetTeamsForSeasonAsync(int apiLeagueId, int seasonYear, CancellationToken cancellationToken);
    Task<IEnumerable<FixtureResponse>> GetAllFixturesForSeasonAsync(int apiLeagueId, int seasonYear, CancellationToken cancellationToken);
    Task<IEnumerable<FixtureResponse>> GetFixturesByRoundAsync(int apiLeagueId, int seasonYear, string apiRoundName, CancellationToken cancellationToken);
    Task<IEnumerable<string>> GetRoundsForSeasonAsync(int apiLeagueId, int seasonYear, CancellationToken cancellationToken);
}