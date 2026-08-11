using System.Diagnostics.CodeAnalysis;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Admin.Seasons.Queries;

/// <summary>One season, with its competition and how many passes have been taken out for it.</summary>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record AdminSeasonRow(
    int Id,
    string Name,
    DateTime StartDateUtc,
    DateTime EndDateUtc,
    bool IsActive,
    int NumberOfRounds,
    int CompetitionId,
    string CompetitionName,
    CompetitionType CompetitionType,
    int? ApiLeagueId,
    decimal? PassStandardPrice,
    decimal? PassPremiumPrice,
    int PassHolderCount);
