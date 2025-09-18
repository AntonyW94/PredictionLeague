using System.Text.Json.Serialization;

namespace PredictionLeague.Application.FootballApi.DTOs;

public class Fixture
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    [JsonPropertyName("date")]
    public DateTime Date { get; set; }
    [JsonPropertyName("status")]
    public Status Status { get; set; }
}