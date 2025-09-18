using System.Text.Json.Serialization;

namespace PredictionLeague.Application.FootballApi.DTOs;

public class Status
{
    [JsonPropertyName("short")]
    public string Short { get; set; }
}