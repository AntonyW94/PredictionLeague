using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Boosts;

[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public record ApplyBoostRequest(int LeagueId, int RoundId, string BoostCode);
