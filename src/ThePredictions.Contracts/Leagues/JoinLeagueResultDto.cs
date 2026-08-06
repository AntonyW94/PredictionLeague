using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Leagues;

/// <summary>Returned after joining a league, so the client can surface payment details for paid leagues.</summary>
[ExcludeFromCodeCoverage]
public record JoinLeagueResultDto(int LeagueId);
