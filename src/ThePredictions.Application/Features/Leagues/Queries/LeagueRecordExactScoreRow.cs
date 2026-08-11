using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// One player's exact-score tally for one round of the league's season.
/// </summary>
/// <remarks>
/// Exact scores live in <c>RoundResults</c>, which is league-agnostic, so these rows are scoped to the league's
/// approved members - as the old statement's two exact-score blocks were. Summing them and finding the best round
/// are both C# now.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record LeagueRecordExactScoreRow(
    string UserId,
    string FirstName,
    string LastName,
    int RoundNumber,
    int ExactScoreCount);
