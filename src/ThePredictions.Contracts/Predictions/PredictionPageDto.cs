using System.Diagnostics.CodeAnalysis;
using ThePredictions.Contracts.Dashboard;

namespace ThePredictions.Contracts.Predictions;

[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public class PredictionPageDto
{
    public int RoundId { get; init; }
    public int RoundNumber { get; init; }
    /// <summary>What the round is called: the name an administrator gave it, or "Round N" where nobody has.</summary>
    /// <remarks>
    /// Worked out by the handler through the one rule the rest of the site uses. This page used to hold its own version -
    /// the stored name for a tournament round and the number for everything else - so a round stored as "Gameweek 5"
    /// appeared here as "Round 5" and as "Gameweek 5" everywhere it was named.
    /// </remarks>
    public string RoundName { get; init; } = string.Empty;
    public string SeasonName { get; init; } = string.Empty;
    public DateTime DeadlineUtc { get; init; }
    public bool IsTournament { get; init; }
    public bool IsLastRoundOfSeason { get; init; }
    public List<MatchPredictionDto> Matches { get; init; } = [];
    public List<PredictionLeagueDto> Leagues { get; init; } = [];
}
