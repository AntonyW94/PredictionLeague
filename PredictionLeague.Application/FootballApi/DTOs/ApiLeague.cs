using System.Text.Json.Serialization;

namespace PredictionLeague.Application.FootballApi.DTOs;

public class ApiLeague
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("round")]
    public string RoundName { get; set; } = null!;
}