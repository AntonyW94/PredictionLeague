using System.Diagnostics.CodeAnalysis;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Contracts.Admin.Matches;

[ExcludeFromCodeCoverage]
public record MatchResultDto(int MatchId, int HomeScore, int AwayScore, MatchStatus Status);
