namespace ThePredictions.Contracts.Prizes;

/// <summary>
/// What a prospective member sees before joining: headline league facts, the projected breakdown
/// once they join, and the attributed "+£x where your money goes" effect of their own entry.
/// Contains numbers and the organiser's name only - no other member identities.
/// </summary>
public class PrizePreviewDto
{
    public string LeagueName { get; init; } = string.Empty;
    public string AdministratorName { get; init; } = string.Empty;
    public int EntrantCount { get; init; }
    public decimal EntryCost { get; init; }
    public decimal CurrentPrizePot { get; init; }
    public decimal ProjectedPrizePot { get; init; }
    public DateTime EntryDeadlineUtc { get; init; }
    public bool DeadlinePassed { get; init; }
    public bool HasPrizes { get; init; }

    /// <summary>The breakdown the joiner would be entering (at N+1), with per-slot/category deltas filled.</summary>
    public PrizeBreakdownDto Breakdown { get; init; } = new();

    /// <summary>Plain-English attribution lines, e.g. "Your £13 adds £8 to the overall prizes".</summary>
    public List<string> Attribution { get; init; } = [];
}
