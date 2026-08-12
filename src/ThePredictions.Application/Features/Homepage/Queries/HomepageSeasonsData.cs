using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Homepage.Queries;

/// <summary>What <see cref="IHomepageSeasonsQuery"/> returns.</summary>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record HomepageSeasonsData(
    IReadOnlyList<HomepageSeasonRow> Seasons,
    IReadOnlyList<HomepageLeagueRow> Leagues,
    IReadOnlyList<HomepageMembershipRow> Memberships);
