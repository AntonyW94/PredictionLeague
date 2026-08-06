using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Dashboard;

[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public record AdminLeagueSummaryDto(
    int LeagueId,
    string LeagueName,
    DateTime EntryDeadlineUtc,
    int MemberCount,
    int PendingCount,
    decimal Price,
    bool IsFree,
    string? EntryCode
);
