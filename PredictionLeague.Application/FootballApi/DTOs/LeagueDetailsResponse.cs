using System.Text.Json.Serialization;

namespace PredictionLeague.Application.FootballApi.DTOs;

public class LeagueDetailsResponse
{
    [JsonPropertyName("league")]
    public ApiLeague League { get; set; } = null!;

    [JsonPropertyName("seasons")]
    public List<ApiSeason> Seasons { get; set; } = null!;
}