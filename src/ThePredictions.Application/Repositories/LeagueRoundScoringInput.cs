using ThePredictions.Domain.Services;

namespace ThePredictions.Application.Repositories;

/// <summary>
/// One player's round tally alongside what the league they are in pays for it.
/// </summary>
/// <remarks>
/// One row per (league, player): a player in three leagues appears three times, because each league sets its own points
/// and so scores the same predictions differently. Which pairs are in scope - every league running the round's season,
/// and only its approved members - is the read's business. What the pair is worth is
/// <see cref="LeagueScoring.BasePoints"/>.
/// </remarks>
public sealed record LeagueRoundScoringInput(
    int LeagueId,
    string UserId,
    OutcomeCounts Counts,
    int PointsForExactScore,
    int PointsForCorrectResult);
