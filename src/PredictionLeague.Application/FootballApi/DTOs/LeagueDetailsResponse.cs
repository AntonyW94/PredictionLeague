using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace PredictionLeague.Application.FootballApi.DTOs;

[SuppressMessage("ReSharper", "UnusedMember.Global")]
[SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global")]
public class LeagueDetailsResponse
{
    [JsonPropertyName("league")]
    public ApiLeague League { get; set; } = null!;

    [JsonPropertyName("seasons")]
    public List<ApiSeason> Seasons { get; set; } = null!;
}