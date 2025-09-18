using System.Text.Json.Serialization;

namespace PredictionLeague.Application.FootballApi.DTOs;

public class Goals
{
    [JsonPropertyName("home")]
    public int? Home { get; set; }
    [JsonPropertyName("away")]
    public int? Away { get; set; }
}