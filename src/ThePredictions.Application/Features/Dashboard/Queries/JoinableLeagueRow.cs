using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Dashboard.Queries;

/// <summary>
/// A league the player is not in, with everything needed to decide whether it should be offered to them.
/// </summary>
/// <remarks>
/// <see cref="HasEntryCode"/> rather than the code itself. The code is the secret that lets somebody into a private league,
/// and this row describes leagues the player is <b>not</b> a member of - so the fact that one exists is all that may travel.
/// Whether that makes the league "private" is a rule, and it is one the handler states.
///
/// <see cref="EntryDeadlineUtc"/> is nullable, as the column is. The old statements filtered on it in SQL, where
/// <c>NULL &gt; GETUTCDATE()</c> quietly excluded a league with no deadline; that is now <c>LeagueEntry.IsOpen</c>.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record JoinableLeagueRow(
    int LeagueId,
    string Name,
    string SeasonName,
    DateTime SeasonStartDateUtc,
    decimal Price,
    decimal? PrizeFundOverride,
    DateTime? EntryDeadlineUtc,
    bool HasEntryCode,
    bool IsListed,
    int MemberCount,
    bool HasSeasonPass);
