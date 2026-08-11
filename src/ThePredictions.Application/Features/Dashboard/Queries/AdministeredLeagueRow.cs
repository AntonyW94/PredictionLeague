using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Dashboard.Queries;

/// <summary>
/// One league the player runs, with the two counts its summary shows.
/// </summary>
/// <remarks>
/// The entry code is returned here, unlike on the league-discovery rows: this is the administrator's own league, and the code
/// is theirs to share.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record AdministeredLeagueRow(
    int LeagueId,
    string LeagueName,
    DateTime? EntryDeadlineUtc,
    int MemberCount,
    int PendingCount,
    decimal Price,
    bool IsFree,
    string? EntryCode);
