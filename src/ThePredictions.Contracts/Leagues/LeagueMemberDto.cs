using System.Diagnostics.CodeAnalysis;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Contracts.Leagues;

[ExcludeFromCodeCoverage]
public record LeagueMemberDto(
    string UserId,
    string FullName, 
    DateTime JoinedAtUtc,
    LeagueMemberStatus Status);
