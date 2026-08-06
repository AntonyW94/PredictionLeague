using System.Diagnostics.CodeAnalysis;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Contracts.Leagues;

[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public record LeagueMemberDto(
    string UserId,
    string FullName, 
    DateTime JoinedAtUtc,
    LeagueMemberStatus Status);
