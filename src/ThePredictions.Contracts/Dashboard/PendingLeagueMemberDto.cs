using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Dashboard;

[ExcludeFromCodeCoverage]
public record PendingLeagueMemberDto(
    int LeagueId,
    string LeagueName,
    string UserId,
    string MemberName,
    DateTime JoinedAtUtc
);
