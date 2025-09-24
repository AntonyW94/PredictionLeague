using System.Text.Json.Serialization;

namespace PredictionLeague.Application.FootballApi.DTOs;

public class FixtureResponse
{
    [JsonPropertyName("fixture")]
    public Fixture? Fixture { get; set; }
    
    [JsonPropertyName("league")]
    public ApiLeague? League { get; set; }
   
    [JsonPropertyName("teams")]
    public Teams? Teams { get; set; }
    
    [JsonPropertyName("goals")]
    public Goals? Goals { get; set; }
}