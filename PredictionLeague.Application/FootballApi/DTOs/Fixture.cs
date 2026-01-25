using System.Text.Json.Serialization;

namespace PredictionLeague.Application.FootballApi.DTOs;

public class Fixture
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
  
    [JsonPropertyName("timezone")]
    public string TimeZone { get; set; } = null!;

    [JsonPropertyName("date")]
    public DateTimeOffset Date { get; set; }

    [JsonPropertyName("status")]
    public Status Status { get; set; } = null!;
}