using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace ThePredictions.Application.FootballApi.DTOs;

[ExcludeFromCodeCoverage(Justification = "Football API response shape: properties only, deserialised straight from the provider.")]
public class FixtureResponseWrapper
{
    [JsonPropertyName("response")]
    public FixtureResponse[] Response { get; init; } = null!;
}