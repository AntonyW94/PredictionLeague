using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// The facts behind a league's winnings page.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record WinningsData(
    WinningsHeaderRow Header,
    IReadOnlyList<WinningsPrizeSettingRow> PrizeSettings,
    IReadOnlyList<WinningsRow> Winnings,
    IReadOnlyList<LeaderboardParticipantRow> Members);
