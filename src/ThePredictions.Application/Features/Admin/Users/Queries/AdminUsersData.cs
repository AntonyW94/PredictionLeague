using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Admin.Users.Queries;

/// <summary>What <see cref="IAdminUsersQuery"/> returns.</summary>
/// <remarks>
/// <see cref="UserIdsWithPayoutDetails"/> is a list of ids rather than a row type because there is nothing else about a
/// payout-details row this screen may see. The account name, sort code and account number are encrypted at rest and are
/// decrypted only for the player themselves and for the administrators of prize leagues they belong to. An administrator
/// reading a list of accounts is neither of those, so the read never asks for the columns - it asks whether a row exists,
/// which is the same question the dashboard checklist asks.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record AdminUsersData(
    IReadOnlyList<AdminUserRow> Users,
    IReadOnlyList<UserLoginProviderRow> LoginProviders,
    IReadOnlyList<UserLeagueRow> Leagues,
    IReadOnlyList<UserSeasonPassRow> SeasonPasses,
    IReadOnlyList<UserWinningRow> Winnings,
    IReadOnlyList<UserSeasonRow> Seasons,
    IReadOnlyList<string> UserIdsWithPayoutDetails,
    IReadOnlyList<UserOnboardingSkipRow> OnboardingSkips,
    IReadOnlyList<UserBadgeRow> Badges);
