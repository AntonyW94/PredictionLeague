using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Boosts;

[ExcludeFromCodeCoverage]
public record ApplyBoostRequest(int LeagueId, int RoundId, string BoostCode);
