using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace PredictionLeague.Application.FootballApi.DTOs;

[SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global")]
public class Status
{
    [JsonPropertyName("short")]
    public string Short { get; set; } = null!;
}