using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.External.Tasks.Queries;

/// <summary>One league whose entry has just closed, with the season facts the welcome email quotes.</summary>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record WelcomeLeagueRow(
    int LeagueId,
    string LeagueName,
    string SeasonName,
    bool HasPrizes,
    int MemberCount,
    int NumberOfRounds,
    DateTime SeasonStartDateUtc,
    DateTime SeasonEndDateUtc);
