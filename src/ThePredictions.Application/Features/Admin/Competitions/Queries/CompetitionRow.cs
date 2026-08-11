using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Admin.Competitions.Queries;

/// <summary>One competition, with how many seasons have been run under it.</summary>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record CompetitionRow(
    int Id,
    string Code,
    string Name,
    int Type,
    string? LogoUrl,
    string? Description,
    int? ApiLeagueId,
    int SeasonCount);
