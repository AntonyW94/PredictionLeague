using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Rounds.Queries;

/// <summary>
/// The team names for one fixture, which the domain entity does not hold. Kept beside the round rather than
/// inside it so <see cref="Domain.Models.Round.Matches"/> stays the single list of fixtures.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record RoundFixtureTeams(string? HomeTeamName, string? AwayTeamName);
