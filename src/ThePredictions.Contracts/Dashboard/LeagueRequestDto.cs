using System.Diagnostics.CodeAnalysis;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Contracts.Dashboard;

[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public record LeagueRequestDto(
    int LeagueId,
    string LeagueName,
    string SeasonName,
    LeagueMemberStatus Status,
    DateTime JoinedAtUtc,
    DateTime? EntryDeadlineUtc,
    string AdminName,
    int MemberCount,
    decimal EntryFee,
    decimal PotValue
);
