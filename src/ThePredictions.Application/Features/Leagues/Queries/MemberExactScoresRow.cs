using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// One member's exact-score count for one round, unaggregated.
/// </summary>
/// <remarks>
/// Not <see cref="MemberRoundPointsRow"/>: that carries boosted points, and reusing it here would put exact
/// scores in a property called <c>BoostedPoints</c>. Names are reused where they fit and not where they do not.
///
/// The count itself comes from <c>RoundResults.ExactScoreCount</c>, a stored aggregate maintained on the write
/// path. Reading it is a fetch; totalling it across the season is a rule and happens in C#.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record MemberExactScoresRow(string UserId, int ExactScoreCount);
