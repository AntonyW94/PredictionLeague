using System.Text.Json;
using ThePredictions.Domain.Services.Prizes;

namespace ThePredictions.Application.Common.Prizes;

/// <summary>
/// Converts a per-league places-table override between its JSON storage form and the domain
/// <see cref="RankTable"/>. Deserialisation builds a validated <see cref="RankTable"/>, so invalid
/// overrides surface as <see cref="ArgumentException"/> (used by the scheme validator).
/// </summary>
public static class RankTableSerializer
{
    private sealed record RankBandPayload(int MinEntrants, int? MaxEntrants, int[] Percentages);

    public static string Serialize(RankTable table)
    {
        var payload = table.Bands
            .Select(b => new RankBandPayload(b.MinEntrants, b.MaxEntrants, b.Percentages.ToArray()))
            .ToList();

        return JsonSerializer.Serialize(payload);
    }

    public static RankTable Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("The rank-table override JSON is empty.", nameof(json));

        List<RankBandPayload>? payload;
        try
        {
            payload = JsonSerializer.Deserialize<List<RankBandPayload>>(json);
        }
        catch (JsonException exception)
        {
            throw new ArgumentException("The rank-table override JSON is not valid.", nameof(json), exception);
        }

        if (payload is null || payload.Count == 0)
            throw new ArgumentException("The rank-table override must contain at least one band.", nameof(json));

        var bands = payload.Select(p => new RankBand(p.MinEntrants, p.MaxEntrants, p.Percentages ?? [])).ToList();
        return new RankTable(bands);
    }
}
