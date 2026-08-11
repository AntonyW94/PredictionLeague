using System.Diagnostics.CodeAnalysis;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// One season a league could belong to.
/// </summary>
/// <remarks>
/// <see cref="CompetitionType"/> rather than an "is it a tournament" flag: the old statement's
/// <c>CASE WHEN c.[Type] = 1</c> asked the question on the caller's behalf, and there are two callers who ask it
/// differently.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record SeasonLookupRow(
    int Id,
    string Name,
    DateTime StartDateUtc,
    bool IsActive,
    CompetitionType CompetitionType);
