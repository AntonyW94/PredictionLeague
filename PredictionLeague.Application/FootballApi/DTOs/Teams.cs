using System.Text.Json.Serialization;

namespace PredictionLeague.Application.FootballApi.DTOs;

public class Teams
{
    [JsonPropertyName("home")]
    public ApiTeam Home { get; set; } = null!;
    [JsonPropertyName("away")]
    public ApiTeam Away { get; set; } = null!;
}