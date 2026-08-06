using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace ThePredictions.Application.FootballApi.DTOs;

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
[ExcludeFromCodeCoverage(Justification = "Football API response shape: properties only, deserialised straight from the provider.")]
public class Goals
{
    [JsonPropertyName("home")]
    public int? Home { get; set; }
    [JsonPropertyName("away")]
    public int? Away { get; set; }
}