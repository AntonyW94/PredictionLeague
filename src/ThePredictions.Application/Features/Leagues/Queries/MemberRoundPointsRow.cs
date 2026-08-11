using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// One member's points for one round, unaggregated.
///
/// The query used to return <c>COALESCE(SUM(BoostedPoints), 0)</c> per member. Summing is computing what the
/// rows mean rather than choosing which rows to read, so it moved to C# with the ranking - and a member with
/// no rows at all scores zero rather than being left off the table, which is the rule the COALESCE encoded.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record MemberRoundPointsRow(string UserId, int BoostedPoints);
