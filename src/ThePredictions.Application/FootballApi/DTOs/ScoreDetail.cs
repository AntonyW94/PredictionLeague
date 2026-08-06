using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace ThePredictions.Application.FootballApi.DTOs;

[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class ScoreDetail
{
    [JsonPropertyName("home")]
    public int? Home { get; set; }

    [JsonPropertyName("away")]
    public int? Away { get; set; }
}
