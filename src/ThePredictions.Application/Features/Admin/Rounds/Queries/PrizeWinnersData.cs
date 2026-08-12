using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Admin.Rounds.Queries;

/// <summary>What <see cref="IPrizeWinnersQuery"/> returns.</summary>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record PrizeWinnersData(
    IReadOnlyList<PrizeWinningRow> Winnings,
    IReadOnlyList<PrizeNotificationRow> Notifications,
    IReadOnlyList<SeasonRoundNameRow> SeasonRounds);
