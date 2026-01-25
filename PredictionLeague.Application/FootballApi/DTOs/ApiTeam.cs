using System.Text.Json.Serialization;

namespace PredictionLeague.Application.FootballApi.DTOs;

public class ApiTeam
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;
}