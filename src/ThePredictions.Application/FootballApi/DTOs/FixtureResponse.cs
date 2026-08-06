using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace ThePredictions.Application.FootballApi.DTOs;

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
[ExcludeFromCodeCoverage(Justification = "Football API response shape: properties only, deserialised straight from the provider.")]
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

    [JsonPropertyName("score")]
    public Score? Score { get; set; }
}