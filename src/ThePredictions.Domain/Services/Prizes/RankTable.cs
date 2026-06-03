using Ardalis.GuardClauses;

namespace ThePredictions.Domain.Services.Prizes;

/// <summary>
/// An ordered places table: a set of non-overlapping <see cref="RankBand"/>s keyed by entrant
/// count. The apportionment engine asks it how many places light up (and their split) for a
/// given entrant count. When no band matches a (small) entrant count, a single winner-takes-all
/// place is assumed.
/// </summary>
public sealed class RankTable
{
    private static readonly IReadOnlyList<int> WinnerTakesAll = new[] { 100 };

    private readonly List<RankBand> _bands;

    public IReadOnlyList<RankBand> Bands => _bands;

    public RankTable(IEnumerable<RankBand> bands)
    {
        Guard.Against.Null(bands);

        _bands = bands.OrderBy(b => b.MinEntrants).ToList();

        if (_bands.Count == 0)
            throw new ArgumentException("A rank table must contain at least one band.", nameof(bands));

        for (var i = 1; i < _bands.Count; i++)
        {
            var previous = _bands[i - 1];
            var current = _bands[i];

            if (previous.MaxEntrants is null)
                throw new ArgumentException("Only the final rank band may be open-ended.", nameof(bands));

            if (current.MinEntrants <= previous.MaxEntrants)
                throw new ArgumentException("Rank bands must not overlap.", nameof(bands));
        }
    }

    /// <summary>
    /// The percentage split for the band matching <paramref name="entrantCount"/>, or a single
    /// winner-takes-all place when no band applies (e.g. very low entrant counts).
    /// </summary>
    public IReadOnlyList<int> PercentagesFor(int entrantCount)
    {
        var band = _bands.FirstOrDefault(b => b.Contains(entrantCount));
        return band?.Percentages ?? WinnerTakesAll;
    }
}
