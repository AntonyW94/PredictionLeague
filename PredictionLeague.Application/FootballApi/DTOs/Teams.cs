using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace PredictionLeague.Application.FootballApi.DTOs;

[SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global")]
public class Teams
{
    [JsonPropertyName("home")]
    public ApiTeam Home { get; set; } = null!;
    [JsonPropertyName("away")]
    public ApiTeam Away { get; set; } = null!;
}