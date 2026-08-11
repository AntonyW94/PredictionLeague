using System.Diagnostics.CodeAnalysis;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Dashboard.Queries;

/// <summary>
/// One request the player has made to join a league, and the league it was made to.
/// </summary>
/// <remarks>
/// <see cref="EntryDeadlineUtc"/> is nullable because the column is, and this read has no deadline filter to hide that: the
/// old result type declared it non-nullable, so a league without a deadline would have failed to materialise and taken the
/// player's dashboard down with it.
///
/// The administrator's name arrives in parts, and the pot's ingredients rather than the pot.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record MyLeagueRequestRow(
    int LeagueId,
    string LeagueName,
    string SeasonName,
    LeagueMemberStatus Status,
    bool IsAlertDismissed,
    DateTime JoinedAtUtc,
    DateTime? EntryDeadlineUtc,
    string AdminFirstName,
    string AdminLastName,
    int MemberCount,
    decimal Price,
    decimal? PrizeFundOverride);
