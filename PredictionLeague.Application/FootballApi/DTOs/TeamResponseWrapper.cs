using System.Text.Json.Serialization;

namespace PredictionLeague.Application.FootballApi.DTOs;

public class TeamResponseWrapper
{
    [JsonPropertyName("response")]
    public TeamResponse[] Response { get; set; }
}