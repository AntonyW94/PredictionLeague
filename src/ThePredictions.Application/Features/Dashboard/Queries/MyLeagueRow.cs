using System.Diagnostics.CodeAnalysis;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Dashboard.Queries;

/// <summary>
/// One league the player is an approved member of, with its season and the four aggregates the tile's arithmetic
/// needs.
/// </summary>
/// <remarks>
/// The aggregates - how many members, how much has been paid out, how much this player has won, how many rounds
/// have finished - are counts and sums, not judgements. What they mean is: the prize pot is
/// <c>PrizeFund.Total</c>, what is left of it is <c>PrizeFund.Remaining</c>, and whether the league is over is
/// <c>SeasonCompletion.IsFinished</c>. All three moved to C#; the counting stayed where the rows are.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record MyLeagueRow(
    int LeagueId,
    string LeagueName,
    decimal Price,
    decimal? PrizeFundOverride,
    bool IsFree,
    bool IsArchivedByUser,
    int SeasonId,
    string SeasonName,
    CompetitionType CompetitionType,
    DateTime SeasonStartDateUtc,
    DateTime? EntryDeadlineUtc,
    int NumberOfRounds,
    int MemberCount,
    int CompletedRoundCount,
    decimal TotalPaidOut,
    decimal UserWinnings);
