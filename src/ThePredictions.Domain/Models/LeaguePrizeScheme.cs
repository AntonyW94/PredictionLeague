using Ardalis.GuardClauses;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Domain.Models;

/// <summary>
/// A league's up-front prize configuration (write-once). It declares which categories pay out and
/// how each entry's stake is split across them; the concrete amounts are derived live by the
/// apportionment engine and frozen into <see cref="LeaguePrizeSetting"/>s at the deadline.
/// Any admin top-up money lives on the league (<see cref="League.PrizeFundOverride"/>), not here.
/// </summary>
public class LeaguePrizeScheme
{
    public int Id { get; init; }
    public int LeagueId { get; private set; }
    public int OverallRoundingThresholdPounds { get; private set; }
    public DateTime SetAtUtc { get; private set; }
    public string SetByUserId { get; private set; } = string.Empty;

    private readonly List<LeaguePrizeSchemeEntry> _entries = new();
    public IReadOnlyCollection<LeaguePrizeSchemeEntry> Entries => _entries.AsReadOnly();

    private LeaguePrizeScheme() { }

    public LeaguePrizeScheme(int id, int leagueId, int overallRoundingThresholdPounds, DateTime setAtUtc, string setByUserId, IEnumerable<LeaguePrizeSchemeEntry?>? entries)
    {
        Id = id;
        LeagueId = leagueId;
        OverallRoundingThresholdPounds = overallRoundingThresholdPounds;
        SetAtUtc = setAtUtc;
        SetByUserId = setByUserId;

        if (entries != null)
            _entries.AddRange(entries.Where(e => e != null).Select(e => e!));
    }

    public static LeaguePrizeScheme Create(
        int stakePounds,
        int overallRoundingThresholdPounds,
        IEnumerable<LeaguePrizeSchemeEntry> entries,
        string setByUserId,
        bool isTournament,
        IDateTimeProvider dateTimeProvider)
    {
        Guard.Against.Negative(stakePounds);
        Guard.Against.Negative(overallRoundingThresholdPounds);
        Guard.Against.NullOrWhiteSpace(setByUserId);
        Guard.Against.Null(entries);

        var entryList = entries.ToList();

        if (entryList.Count == 0)
            throw new ArgumentException("A prize scheme must enable at least one category.", nameof(entries));

        if (entryList.Select(e => e.Category).Distinct().Count() != entryList.Count)
            throw new ArgumentException("A prize scheme cannot enable the same category twice.", nameof(entries));

        var totalAllocated = entryList.Sum(e => e.PerEntryPounds);
        if (totalAllocated != stakePounds)
            throw new ArgumentException("The per-entry allocations must sum to the entry stake.", nameof(entries));

        foreach (var entry in entryList)
        {
            if (entry.Category == PrizeType.Section && !isTournament)
                throw new ArgumentException("Section prizes are only available for tournaments.", nameof(entries));

            if (entry.Category == PrizeType.Monthly && isTournament)
                throw new ArgumentException("Monthly prizes are only available for seasons.", nameof(entries));
        }

        var scheme = new LeaguePrizeScheme
        {
            OverallRoundingThresholdPounds = overallRoundingThresholdPounds,
            SetByUserId = setByUserId,
            SetAtUtc = dateTimeProvider.UtcNow
        };

        scheme._entries.AddRange(entryList);
        return scheme;
    }

    public void AssignToLeague(int leagueId)
    {
        Guard.Against.NegativeOrZero(leagueId);
        LeagueId = leagueId;
    }
}
