using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Homepage.Queries;

/// <summary>One league of a season, with what it costs to enter and whatever its administrator has put in.</summary>
/// <remarks>
/// The member count is here rather than the members themselves because the pot is an entry fee times a head count. The
/// memberships come back separately for the player total, which counts a person once however many of the season's leagues they
/// are in.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record HomepageLeagueRow(
    int SeasonId,
    int LeagueId,
    decimal Price,
    decimal? PrizeFundOverride,
    int ApprovedMemberCount);
