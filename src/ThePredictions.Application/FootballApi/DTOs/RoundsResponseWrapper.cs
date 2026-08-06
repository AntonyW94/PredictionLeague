using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace ThePredictions.Application.FootballApi.DTOs;

[ExcludeFromCodeCoverage(Justification = "Football API response shape: properties only, deserialised straight from the provider.")]
public class RoundsResponseWrapper
{
    [JsonPropertyName("response")]
    public string[] Response { get; init; } = null!;
}