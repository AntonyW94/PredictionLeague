using System.Text.Json.Serialization;

namespace PredictionLeague.Application.FootballApi.DTOs;

public class FixtureResponseWrapper
{
    [JsonPropertyName("response")]
    public FixtureResponse[] Response { get; init; } = null!;
}