using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Boosts.Queries;

/// <summary>
/// One boost a player has played, as the database holds it and <b>uncensored</b>.
///
/// Two things are deliberately raw. <see cref="RoundDeadlineUtc"/> is carried so the secrecy rule can be
/// applied in C# against an injected clock - the predicate used to be SQL comparing against
/// <c>GETUTCDATE()</c>, which no test could pin to an instant. And the three scoring fields are carried
/// instead of a computed points-gained figure, because what a boost won is a scoring rule rather than
/// something to be worked out in a <c>CASE</c> expression.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record BoostUsageRow(
    string UserId,
    string BoostCode,
    int RoundNumber,
    DateTime RoundDeadlineUtc,
    bool HasBoost,
    int? BasePoints,
    int? BoostedPoints);
