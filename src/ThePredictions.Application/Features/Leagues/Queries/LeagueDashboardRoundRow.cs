using System.Diagnostics.CodeAnalysis;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// One round of the league's season, with how many fixtures it holds.
/// </summary>
/// <remarks>
/// Unordered. The dashboard shows the newest round first, which is a presentation rule and belongs with the handler
/// rather than in an <c>ORDER BY</c> nobody can test.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record LeagueDashboardRoundRow(
    int RoundId,
    int SeasonId,
    int RoundNumber,
    string? ApiRoundName,
    DateTime StartDateUtc,
    DateTime DeadlineUtc,
    RoundStatus Status,
    int MatchCount);
