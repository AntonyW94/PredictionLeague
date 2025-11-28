using System.Text.Json.Serialization;

namespace PredictionLeague.Application.FootballApi.DTOs;

public class ApiSeason
{
    [JsonPropertyName("year")]
    public int Year { get; set; }

    [JsonPropertyName("start")]
    public DateTime Start { get; set; }

    [JsonPropertyName("end")]
    public DateTime End { get; set; }
}