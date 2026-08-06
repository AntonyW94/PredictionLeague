using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Rounds;

/// <summary>A confirmed fixture in the round that a player has not yet predicted.</summary>
[ExcludeFromCodeCoverage]
public record MissingFixtureDto(int MatchId, int? MatchNumber, string HomeTeam, string AwayTeam);
