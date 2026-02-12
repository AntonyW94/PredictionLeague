using System.Text.Json.Serialization;

namespace PredictionLeague.Application.FootballApi.DTOs;

public class LeagueDetailsResponseWrapper
{
    [JsonPropertyName("response")]
    public LeagueDetailsResponse[] Response { get; init; } = null!;
}