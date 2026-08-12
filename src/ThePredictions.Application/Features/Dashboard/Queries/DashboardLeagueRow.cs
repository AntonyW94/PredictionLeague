using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Dashboard.Queries;

/// <summary>
/// One league the player belongs to, with the four season facts the tile's rules need and the player's own
/// archive flag.
/// </summary>
/// <remarks>
/// <see cref="CompletedRoundCount"/> and <see cref="NumberOfRounds"/> come back separately rather than as an
/// <c>IsFinished</c> flag: how many rounds have finished and how many the season declares are both facts, while
/// comparing them is <c>SeasonCompletion.IsFinished</c> - and the comparison is <c>&gt;=</c> rather than
/// <c>=</c> for a reason worth stating once in C#.
///
/// The counting itself stays in the adapter. Returning every round of every one of a player's seasons only to
/// count them would be a lot of rows to answer two questions, and neither answer is a judgement: contrast the
/// stage leaderboard, which needs each round's identity to know which stage it belongs to.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record DashboardLeagueRow(
    int LeagueId,
    string LeagueName,
    decimal Price,
    string SeasonName,
    DateTime SeasonStartDateUtc,
    int SeasonRoundCount,
    int CompletedRoundCount,
    bool HasRoundInProgress,
    bool IsArchivedByUser) : ILeagueTile;
