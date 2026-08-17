using Ardalis.GuardClauses;
using MediatR;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Admin.Users;
using ThePredictions.Domain.Common.Guards;

namespace ThePredictions.Application.Features.Admin.Users.Queries;

/// <summary>
/// What deleting one account would destroy, so the administrator is told before being asked to confirm.
/// </summary>
/// <remarks>
/// The account is looked up first so an unknown id is a 404. Without that the read would happily return a
/// row of zeroes for an id that has never existed, and the dialog would offer to delete nothing at all.
/// </remarks>
public class GetUserDeletionImpactQueryHandler(
    IUserManager userManager,
    IUserDeletionImpactQuery userDeletionImpactQuery)
    : IRequestHandler<GetUserDeletionImpactQuery, UserDeletionImpactDto>
{
    public async Task<UserDeletionImpactDto> Handle(GetUserDeletionImpactQuery request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(request.UserId);
        Guard.Against.EntityNotFound(request.UserId, user, "User");

        var impact = await userDeletionImpactQuery.ExecuteAsync(request.UserId, cancellationToken);

        return new UserDeletionImpactDto(
            impact.SeasonPasses,
            impact.SeasonPassSpend,
            impact.LeagueMemberships,
            impact.Predictions,
            impact.Winnings,
            impact.WinningsTotal,
            impact.Payouts,
            impact.PayoutsTotal,
            impact.Badges,
            impact.BoostUsages,
            impact.RoundResults,
            impact.LeagueRoundResults,
            impact.LeagueStandings,
            impact.EmailRecords,
            impact.OnboardingSkips,
            impact.HasPayoutDetails,
            impact.LeaguesAdministered);
    }
}
