using System.Diagnostics.CodeAnalysis;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Homepage.Queries;

/// <summary>One season, with the dates the homepage judges it by.</summary>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record HomepageSeasonRow(
    int Id,
    string Name,
    CompetitionType CompetitionType,
    DateTime StartDateUtc,
    DateTime EndDateUtc);
