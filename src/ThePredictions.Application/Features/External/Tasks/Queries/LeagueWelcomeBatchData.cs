using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.External.Tasks.Queries;

/// <summary>What <see cref="ILeagueWelcomeBatchQuery"/> returns.</summary>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record LeagueWelcomeBatchData(
    IReadOnlyList<WelcomeLeagueRow> Leagues,
    IReadOnlyList<WelcomeRecipientRow> Recipients,
    IReadOnlyList<WelcomeNotificationRow> AlreadyNotified,
    IReadOnlyList<WelcomeSchemeRow> Schemes,
    IReadOnlyList<WelcomePrizeRow> Prizes,
    IReadOnlyList<WelcomeBoostRow> Boosts,
    IReadOnlyList<WelcomeBoostWindowRow> BoostWindows);
