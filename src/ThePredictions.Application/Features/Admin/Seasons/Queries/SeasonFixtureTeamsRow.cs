using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Admin.Seasons.Queries;

/// <summary>One fixture, as the two team ids it is between. Either can be missing before a knockout tie is settled.</summary>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record SeasonFixtureTeamsRow(int SeasonId, int RoundNumber, int? HomeTeamId, int? AwayTeamId);
