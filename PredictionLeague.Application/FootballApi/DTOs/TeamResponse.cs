using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace PredictionLeague.Application.FootballApi.DTOs;

[SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global")]
public class TeamResponse
{
    [JsonPropertyName("team")]
    public ApiTeam Team { get; set; } = null!;
}