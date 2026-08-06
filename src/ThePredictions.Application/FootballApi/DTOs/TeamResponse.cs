using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace ThePredictions.Application.FootballApi.DTOs;

[SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global")]
[ExcludeFromCodeCoverage(Justification = "Football API response shape: properties only, deserialised straight from the provider.")]
public class TeamResponse
{
    [JsonPropertyName("team")]
    public ApiTeam Team { get; set; } = null!;
}