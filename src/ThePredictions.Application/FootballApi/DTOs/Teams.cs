using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace ThePredictions.Application.FootballApi.DTOs;

[SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global")]
[ExcludeFromCodeCoverage(Justification = "Football API response shape: properties only, deserialised straight from the provider.")]
public class Teams
{
    [JsonPropertyName("home")]
    public ApiTeam Home { get; set; } = null!;
    [JsonPropertyName("away")]
    public ApiTeam Away { get; set; } = null!;
}