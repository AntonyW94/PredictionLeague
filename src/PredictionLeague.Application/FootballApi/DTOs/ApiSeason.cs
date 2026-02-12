using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace PredictionLeague.Application.FootballApi.DTOs;

[SuppressMessage("ReSharper", "UnusedMember.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class ApiSeason
{
    [JsonPropertyName("year")]
    public int Year { get; set; }

    [JsonPropertyName("start")]
    public DateTime Start { get; set; }

    [JsonPropertyName("end")]
    public DateTime End { get; set; }
}