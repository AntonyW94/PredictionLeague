namespace ThePredictions.Domain.Common.Enumerations;

/// <summary>
/// Classifies how a prize category is scored and scheduled. Drives apportionment behaviour
/// and availability gating, independent of the concrete <see cref="PrizeType"/>.
/// </summary>
public enum PrizeCategoryKind
{
    /// <summary>Settled once at the end of the season (e.g. Overall, Most Exact Scores).</summary>
    EndOfSeason,

    /// <summary>Awarded repeatedly per event - per round or per month (e.g. Round, Monthly).</summary>
    Recurring,

    /// <summary>Settled per tournament stage - group stage vs knockouts (e.g. Section).</summary>
    Staged
}
