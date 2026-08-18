using System.Diagnostics.CodeAnalysis;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Admin.Users.Queries;

/// <summary>
/// One league an account is involved in - as its administrator, as a member, or both.
/// </summary>
/// <remarks>
/// <see cref="Status"/> is null when the account administers the league without being a member of it, which the schema
/// allows. Price and the free flag come back because what counts as money spent on league entry is a rule.
///
/// <see cref="ApprovedMemberCount"/> carries the same figure on both kinds of row and only means anything on the
/// administrator's, where it says how many people are in a league they run. Counting it once per league inside the read
/// beats counting it per account in the handler, which would need everybody else's memberships loaded as well.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record UserLeagueRow(
    string UserId,
    int LeagueId,
    string LeagueName,
    int SeasonId,
    bool IsAdministrator,
    LeagueMemberStatus? Status,
    bool IsFree,
    decimal Price,
    int ApprovedMemberCount);
