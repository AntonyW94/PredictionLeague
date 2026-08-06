using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ThePredictions.Application.Configuration;
using ThePredictions.Application.FootballApi.DTOs;
using ThePredictions.Application.Services;
using System.Net.Http.Json;

namespace ThePredictions.Infrastructure.Services;

[ExcludeFromCodeCoverage(Justification = "Third-party API client: a thin call into an external SDK, verified against the live service.")]
public class FootballDataService : IFootballDataService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FootballDataService> _logger;

    public FootballDataService(
        HttpClient httpClient,
        IOptions<FootballApiSettings> apiSettings,
        ILogger<FootballDataService> logger)
    {
        var settings = apiSettings.Value;

        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(settings.BaseUrl);
        _httpClient.DefaultRequestHeaders.Add("x-apisports-key", settings.ApiKey);
        _logger = logger;
    }

    public async Task<IEnumerable<TeamResponse>> GetTeamsForSeasonAsync(int apiLeagueId, int seasonYear, CancellationToken cancellationToken)
    {
        var endpoint = $"teams?league={apiLeagueId}&season={seasonYear}";
        var wrapper = await _httpClient.GetFromJsonAsync<TeamResponseWrapper>(endpoint, cancellationToken);

        if (wrapper?.Response == null)
        {
            _logger.LogWarning("Football API returned null teams response for League (ID: {ApiLeagueId}), Season {SeasonYear}",
                apiLeagueId, seasonYear);
            return Enumerable.Empty<TeamResponse>();
        }

        return wrapper.Response;
    }

    public async Task<IEnumerable<FixtureResponse>> GetAllFixturesForSeasonAsync(int apiLeagueId, int seasonYear, CancellationToken cancellationToken)
    {
        var endpoint = $"fixtures?league={apiLeagueId}&season={seasonYear}";
        var wrapper = await _httpClient.GetFromJsonAsync<FixtureResponseWrapper>(endpoint, cancellationToken);

        if (wrapper?.Response == null)
        {
            _logger.LogWarning("Football API returned null fixtures response for League (ID: {ApiLeagueId}), Season {SeasonYear}",
                apiLeagueId, seasonYear);
            return Enumerable.Empty<FixtureResponse>();
        }

        return wrapper.Response;
    }

    public async Task<IEnumerable<FixtureResponse>> GetFixturesByIdsAsync(List<int>? fixtureIds, CancellationToken cancellationToken)
    {
        if (fixtureIds == null || fixtureIds.Count == 0)
            return Enumerable.Empty<FixtureResponse>();

        var idsString = string.Join("-", fixtureIds);
        var endpoint = $"fixtures?ids={idsString}";

        var wrapper = await _httpClient.GetFromJsonAsync<FixtureResponseWrapper>(endpoint, cancellationToken);

        if (wrapper?.Response == null)
        {
            _logger.LogWarning("Football API returned null fixtures response for fixture IDs: {FixtureIds}", idsString);
            return Enumerable.Empty<FixtureResponse>();
        }

        return wrapper.Response;
    }

    public async Task<IEnumerable<string>> GetRoundsForSeasonAsync(int apiLeagueId, int seasonYear, CancellationToken cancellationToken)
    {
        var endpoint = $"fixtures/rounds?league={apiLeagueId}&season={seasonYear}";
        var wrapper = await _httpClient.GetFromJsonAsync<RoundsResponseWrapper>(endpoint, cancellationToken);

        if (wrapper?.Response == null)
        {
            _logger.LogWarning("Football API returned null rounds response for League (ID: {ApiLeagueId}), Season {SeasonYear}",
                apiLeagueId, seasonYear);
            return Enumerable.Empty<string>();
        }

        return wrapper.Response;
    }

    public async Task<ApiSeason> GetLeagueSeasonDetailsAsync(int apiLeagueId, int seasonYear, CancellationToken cancellationToken)
    {
        var (_, season) = await GetLeagueInfoAsync(apiLeagueId, seasonYear, cancellationToken);
        return season;
    }

    public async Task<(string? LeagueName, ApiSeason Season)> GetLeagueInfoAsync(int apiLeagueId, int seasonYear, CancellationToken cancellationToken)
    {
        var endpoint = $"leagues?id={apiLeagueId}&season={seasonYear}";
        var wrapper = await _httpClient.GetFromJsonAsync<LeagueDetailsResponseWrapper>(endpoint, cancellationToken);

        var response = wrapper?.Response.FirstOrDefault();
        var season = response?.Seasons.FirstOrDefault();
        if (season == null)
        {
            _logger.LogWarning("Football API returned null season details for League (ID: {ApiLeagueId}), Season {SeasonYear}",
                apiLeagueId, seasonYear);
            return (null, new ApiSeason());
        }

        return (response?.League.Name, season);
    }
}
