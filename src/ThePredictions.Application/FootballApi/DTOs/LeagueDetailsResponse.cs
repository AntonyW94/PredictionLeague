using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace ThePredictions.Application.FootballApi.DTOs;

[SuppressMessage("ReSharper", "UnusedMember.Global")]
[SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global")]
[ExcludeFromCodeCoverage(Justification = "Football API response shape: properties only, deserialised straight from the provider.")]
public class LeagueDetailsResponse
{
    [JsonPropertyName("league")]
    public ApiLeague League { get; set; } = null!;

    [JsonPropertyName("seasons")]
    public List<ApiSeason> Seasons { get; set; } = null!;
}