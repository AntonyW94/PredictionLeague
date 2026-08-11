using System.Diagnostics.CodeAnalysis;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Admin.Users.Queries;

/// <summary>
/// One league an account is involved in - as its administrator, as a member, or both.
/// </summary>
/// <remarks>
/// <see cref="Status"/> is null when the account administers the league without being a member of it, which the schema
/// allows. Price and the free flag come back because what counts as money spent on league entry is a rule.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record UserLeagueRow(
    string UserId,
    int LeagueId,
    bool IsAdministrator,
    LeagueMemberStatus? Status,
    bool IsFree,
    decimal Price);
