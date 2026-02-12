using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace PredictionLeague.Application.FootballApi.DTOs;


[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
[SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global")]
public class ApiTeam
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;
}