using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Admin.Rounds.Queries;

/// <summary>One player's points in one league for this round.</summary>
/// <remarks>
/// Every scored player in every league of the season, not only the ones being emailed: these rows answer both "what did
/// this player score in this league" and "who topped this league", and the second needs everybody. Choosing the top
/// scorer was a <c>ROW_NUMBER() OVER (PARTITION BY LeagueId ORDER BY BoostedPoints DESC, FirstName)</c>, which is a
/// ranking with a tie-break, and both halves of that are rules.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record RoundLeagueScoreRow(
    int LeagueId,
    string UserId,
    string FirstName,
    string LastName,
    int BoostedPoints);
