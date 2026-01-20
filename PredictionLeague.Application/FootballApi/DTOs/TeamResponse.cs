using System.Text.Json.Serialization;

namespace PredictionLeague.Application.FootballApi.DTOs;

public class TeamResponse
{
    [JsonPropertyName("team")]
    public ApiTeam Team { get; set; } = null!;
}