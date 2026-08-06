using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Dashboard;

[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public record PendingLeagueMemberDto(
    int LeagueId,
    string LeagueName,
    string UserId,
    string MemberName,
    DateTime JoinedAtUtc
);
