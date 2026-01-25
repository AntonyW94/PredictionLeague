using System.Text.Json.Serialization;

namespace PredictionLeague.Application.FootballApi.DTOs;

public class RoundsResponseWrapper
{
    [JsonPropertyName("response")]
    public string[] Response { get; set; } = null!;
}