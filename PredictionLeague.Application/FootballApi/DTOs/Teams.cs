using System.Text.Json.Serialization;

namespace PredictionLeague.Application.FootballApi.DTOs;

public class Teams
{
    [JsonPropertyName("home")]
    public Team Home { get; set; }
    [JsonPropertyName("away")]
    public Team Away { get; set; }
}