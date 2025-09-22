using Microsoft.Extensions.Options;
using PredictionLeague.Application.Configuration;
using PredictionLeague.Application.FootballApi.DTOs;
using PredictionLeague.Application.Services;
using System.Net.Http.Json;

namespace PredictionLeague.Infrastructure.Services;

public class FootballDataService : IFootballDataService
{
    private readonly HttpClient _httpClient;

    public FootballDataService(HttpClient httpClient, IOptions<FootballApiSettings> apiSettings)
    {
        var settings = apiSettings.Value;

        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(settings.BaseUrl);
        _httpClient.DefaultRequestHeaders.Add("x-apisports-key", settings.ApiKey);
    }

    public async Task<IEnumerable<TeamResponse>> GetTeamsForSeasonAsync(int apiLeagueId, int seasonYear, CancellationToken cancellationToken)
    {
        var endpoint = $"teams?league={apiLeagueId}&season={seasonYear}";
        var wrapper = await _httpClient.GetFromJsonAsync<TeamResponseWrapper>(endpoint, cancellationToken);
        return wrapper?.Response ?? Enumerable.Empty<TeamResponse>();
    }

    public async Task<IEnumerable<FixtureResponse>> GetAllFixturesForSeasonAsync(int apiLeagueId, int seasonYear, CancellationToken cancellationToken)
    {
        var endpoint = $"fixtures?league={apiLeagueId}&season={seasonYear}";
        var wrapper = await _httpClient.GetFromJsonAsync<FixtureResponseWrapper>(endpoint, cancellationToken);
        return wrapper?.Response ?? Enumerable.Empty<FixtureResponse>();
    }
        
    public async Task<IEnumerable<FixtureResponse>> GetFixturesByRoundAsync(int apiLeagueId, int seasonYear, string apiRoundName, CancellationToken cancellationToken)
    {
        var endpoint = $"fixtures?league={apiLeagueId}&season={seasonYear}&round={apiRoundName}";
        var wrapper = await _httpClient.GetFromJsonAsync<FixtureResponseWrapper>(endpoint, cancellationToken);

        return wrapper?.Response ?? Enumerable.Empty<FixtureResponse>();
    }

    public async Task<IEnumerable<string>> GetRoundsForSeasonAsync(int apiLeagueId, int seasonYear, CancellationToken cancellationToken)
    {
        var endpoint = $"fixtures/rounds?league={apiLeagueId}&season={seasonYear}";
        var wrapper = await _httpClient.GetFromJsonAsync<RoundsResponseWrapper>(endpoint, cancellationToken);
        return wrapper?.Response ?? Enumerable.Empty<string>();
    }

    public async Task<ApiSeason> GetLeagueSeasonDetailsAsync(int apiLeagueId, int seasonYear, CancellationToken cancellationToken)
    {
        var endpoint = $"leagues?id={apiLeagueId}&season={seasonYear}";
        var wrapper = await _httpClient.GetFromJsonAsync<LeagueDetailsResponseWrapper>(endpoint, cancellationToken);

        return wrapper?.Response?.FirstOrDefault()?.Seasons?.FirstOrDefault() ?? new ApiSeason();
    }
}