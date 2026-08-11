using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Admin.Users.Queries;

/// <summary>What <see cref="IAdminUsersQuery"/> returns.</summary>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record AdminUsersData(
    IReadOnlyList<AdminUserRow> Users,
    IReadOnlyList<UserLoginProviderRow> LoginProviders,
    IReadOnlyList<UserLeagueRow> Leagues,
    IReadOnlyList<UserSeasonPassRow> SeasonPasses,
    IReadOnlyList<UserWinningRow> Winnings);
