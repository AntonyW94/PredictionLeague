using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace ThePredictions.Application.FootballApi.DTOs;

[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class Score
{
    [JsonPropertyName("halftime")]
    public ScoreDetail? HalfTime { get; set; }

    [JsonPropertyName("fulltime")]
    public ScoreDetail? FullTime { get; set; }

    [JsonPropertyName("extratime")]
    public ScoreDetail? ExtraTime { get; set; }

    [JsonPropertyName("penalty")]
    public ScoreDetail? Penalty { get; set; }
}
