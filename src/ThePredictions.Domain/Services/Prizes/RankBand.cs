using Ardalis.GuardClauses;

namespace ThePredictions.Domain.Services.Prizes;

/// <summary>
/// One row of a places table: for an entrant count within [<see cref="MinEntrants"/>,
/// <see cref="MaxEntrants"/>], how the category sub-pot is split across ranks (descending
/// percentages summing to 100). The number of paid places is <c>Percentages.Count</c>.
/// </summary>
public sealed class RankBand
{
    public int MinEntrants { get; }
    public int? MaxEntrants { get; }
    public IReadOnlyList<int> Percentages { get; }

    public RankBand(int minEntrants, int? maxEntrants, IReadOnlyList<int> percentages)
    {
        Guard.Against.NegativeOrZero(minEntrants);
        Guard.Against.Null(percentages);

        if (maxEntrants is not null && maxEntrants < minEntrants)
            throw new ArgumentException("The maximum entrants must be greater than or equal to the minimum entrants.", nameof(maxEntrants));

        if (percentages.Count == 0)
            throw new ArgumentException("A rank band must specify at least one place.", nameof(percentages));

        if (percentages.Any(p => p <= 0))
            throw new ArgumentException("Every place percentage must be greater than zero.", nameof(percentages));

        if (percentages.Sum() != 100)
            throw new ArgumentException("Place percentages must sum to 100.", nameof(percentages));

        for (var i = 1; i < percentages.Count; i++)
        {
            if (percentages[i] > percentages[i - 1])
                throw new ArgumentException("Place percentages must be in descending order.", nameof(percentages));
        }

        MinEntrants = minEntrants;
        MaxEntrants = maxEntrants;
        Percentages = percentages;
    }

    public bool Contains(int entrantCount) => entrantCount >= MinEntrants && (MaxEntrants is null || entrantCount <= MaxEntrants);
}
